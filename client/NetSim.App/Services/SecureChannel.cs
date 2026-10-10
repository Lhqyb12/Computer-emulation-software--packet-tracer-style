using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSim.App.Services;


// Our own encrypted connection, built on top of a plain socket. It replaces TLS (the "S" of HTTPS).
//
// Part 1 - the handshake, once per connection. Both sides end up with the same secret key:
//   server -> client : 32 random bytes (the "server random")
//   client -> server : 32 secret random bytes (the "client secret"), encrypted with the server's RSA public key
//   both sides       : session key = SHA-256(client secret + server random)
//
// Part 2 - the messages. Every message is encrypted with AES-GCM under the session key and travels as:
//   12 bytes nonce | 16 bytes tag | the encrypted text
//
// The client and the server each have a copy of this file, and the two must stay identical
public sealed class SecureChannel : IDisposable
{
    private const int RandomSize = 32;  // the server random, the client secret and the AES-256 key: 32 bytes each
    private const int NonceSize = 12;   // the nonce size AES-GCM works with
    private const int TagSize = 16;     // the authentication tag AES-GCM produces

    // One byte that says which direction a message travels in
    private const byte ClientToServer = 1;
    private const byte ServerToClient = 2;

    private readonly Socket _socket;
    private readonly AesGcm _aes;
    private readonly byte _sendDirection;
    private readonly byte _receiveDirection;

    // How many messages were sent and received on this connection so far
    private long _sendCounter;
    private long _receiveCounter;

    private SecureChannel(Socket socket, byte[] sessionKey, bool isClient)
    {
        _socket = socket;
        _aes = new AesGcm(sessionKey, TagSize);
        _sendDirection = isClient ? ClientToServer : ServerToClient;
        _receiveDirection = isClient ? ServerToClient : ClientToServer;
    }

    // The client's side of the handshake
    public static async Task<SecureChannel> ConnectAsync(Socket socket, RSA serverPublicKey, CancellationToken stop = default)
    {
        // 1. The server speaks first: 32 fresh random bytes
        byte[]? serverRandom = await MessageFraming.ReceiveFrameAsync(socket, stop);
        if (serverRandom is null || serverRandom.Length != RandomSize)
        {
            throw new IOException("The server did not start the handshake correctly.");
        }

        // 2. Choose a secret and send it encrypted with the server's public key.
        //    Only the holder of the matching private key - the real server - can read it
        byte[] clientSecret = RandomNumberGenerator.GetBytes(RandomSize);
        byte[] encryptedSecret = serverPublicKey.Encrypt(clientSecret, RSAEncryptionPadding.OaepSHA256);
        await MessageFraming.SendFrameAsync(socket, encryptedSecret, stop);

        // 3. Both sides compute the same session key
        return new SecureChannel(socket, DeriveSessionKey(clientSecret, serverRandom), isClient: true);
    }

    // The server's side of the handshake. Returns null if the client hung up before finishing it
    public static async Task<SecureChannel?> AcceptAsync(Socket socket, RSA serverPrivateKey, CancellationToken stop = default)
    {
        // 1. Send 32 fresh random bytes. Because they are new for every connection, the session key is
        //    new for every connection too - a recording of an old conversation is useless if sent again
        byte[] serverRandom = RandomNumberGenerator.GetBytes(RandomSize);
        await MessageFraming.SendFrameAsync(socket, serverRandom, stop);

        // 2. Receive the client's secret and open it with our private key
        byte[]? encryptedSecret = await MessageFraming.ReceiveFrameAsync(socket, stop);
        if (encryptedSecret is null)
        {
            return null;
        }

        byte[] clientSecret;
        lock (serverPrivateKey) // one private key object serves all the connections - one at a time
        {
            clientSecret = serverPrivateKey.Decrypt(encryptedSecret, RSAEncryptionPadding.OaepSHA256);
        }

        // 3. Both sides compute the same session key
        return new SecureChannel(socket, DeriveSessionKey(clientSecret, serverRandom), isClient: false);
    }

    // Session key = SHA-256(client secret + server random). SHA-256 always gives 32 bytes - exactly an AES-256 key
    private static byte[] DeriveSessionKey(byte[] clientSecret, byte[] serverRandom)
    {
        byte[] both = new byte[clientSecret.Length + serverRandom.Length];
        clientSecret.CopyTo(both, 0);
        serverRandom.CopyTo(both, clientSecret.Length);
        return SHA256.HashData(both);
    }

    public async Task SendAsync(string text, CancellationToken stop = default)
    {
        // Text -> bytes
        byte[] plain = Encoding.UTF8.GetBytes(text);

        // A nonce is a random value used for one message only. It makes the same text
        // encrypt to different bytes every time
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] encrypted = new byte[plain.Length];
        byte[] tag = new byte[TagSize];

        // Encrypt. The tag is a fingerprint of the message that only a holder of the key can compute.
        // The label (direction + message number) is not sent, but it is mixed into the tag
        _aes.Encrypt(nonce, plain, encrypted, tag, BuildLabel(_sendDirection, _sendCounter));
        _sendCounter++;

        // The frame: nonce | tag | encrypted text
        byte[] frame = new byte[NonceSize + TagSize + encrypted.Length];
        nonce.CopyTo(frame, 0);
        tag.CopyTo(frame, NonceSize);
        encrypted.CopyTo(frame, NonceSize + TagSize);

        await MessageFraming.SendFrameAsync(_socket, frame, stop);
    }

    // Returns the next message, or null if the other side closed the connection
    public async Task<string?> ReceiveAsync(CancellationToken stop = default)
    {
        byte[]? frame = await MessageFraming.ReceiveFrameAsync(_socket, stop);
        if (frame is null)
        {
            return null;
        }

        if (frame.Length < NonceSize + TagSize)
        {
            throw new InvalidDataException("The encrypted frame is too short.");
        }

        // Split the frame back into its three parts
        byte[] nonce = frame[..NonceSize];
        byte[] tag = frame[NonceSize..(NonceSize + TagSize)];
        byte[] encrypted = frame[(NonceSize + TagSize)..];
        byte[] plain = new byte[encrypted.Length];

        // Decrypt checks the tag first. If even one bit was changed on the way, if the message is a
        // copy of an earlier one, or if it was meant for the other direction - it throws and nothing is read
        _aes.Decrypt(nonce, encrypted, tag, plain, BuildLabel(_receiveDirection, _receiveCounter));
        _receiveCounter++;

        // Bytes -> text
        return Encoding.UTF8.GetString(plain);
    }

    // 9 bytes: the direction, then the message number. Both sides build it on their own and never send it
    private static byte[] BuildLabel(byte direction, long counter)
    {
        byte[] label = new byte[9];
        label[0] = direction;
        BinaryPrimitives.WriteInt64BigEndian(label.AsSpan(1), counter);
        return label;
    }

    public void Dispose()
    {
        // Wipes the session key from memory. The socket itself is closed by whoever opened it
        _aes.Dispose();
    }
}
