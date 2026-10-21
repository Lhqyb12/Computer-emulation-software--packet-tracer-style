using System;                        // IDisposable, and AsSpan() that we use on arrays
using System.Buffers.Binary;         // BinaryPrimitives: turns a number into bytes
using System.IO;                     // IOException and InvalidDataException
using System.Net.Sockets;            // The Socket class
using System.Security.Cryptography;  // RSA, AesGcm, SHA256, RandomNumberGenerator
using System.Text;                   // Encoding.UTF8: text <-> bytes
using System.Threading;              // CancellationToken
using System.Threading.Tasks;        // Task, async/await

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
// The client and the server each have a copy of this file, and the two must stay identical.
// "sealed" = no class can inherit from it. "IDisposable" = it has a Dispose method, so it can be
// used in a "using" line that cleans it up automatically
public sealed class SecureChannel : IDisposable
{
    private const int RandomSize = 32;  // the server random, the client secret and the AES-256 key: 32 bytes each
    private const int NonceSize = 12;   // the nonce size AES-GCM works with
    private const int TagSize = 16;     // the authentication tag AES-GCM produces

    // One byte that says which direction a message travels in
    private const byte ClientToServer = 1;
    private const byte ServerToClient = 2;

    // The plain TCP connection that the encrypted frames travel on
    private readonly Socket _socket;

    // The AES-GCM encryptor, already loaded with this connection's session key
    private readonly AesGcm _aes;

    // The direction byte for the messages this side sends
    private readonly byte _sendDirection;

    // The direction byte this side expects on the messages it receives
    private readonly byte _receiveDirection;

    // How many messages were sent and received on this connection so far
    private long _sendCounter;
    private long _receiveCounter;

    // Purpose: Builds a ready channel once the handshake has produced a session key.
    // Input:   socket - the connection; sessionKey - the 32-byte AES key; isClient - which side this is.
    // Output:  none (a constructor) - a new SecureChannel object.
    // The constructor. "private" = a channel can be created only through ConnectAsync or AcceptAsync,
    // so there is never a channel that skipped the handshake
    private SecureChannel(Socket socket, byte[] sessionKey, bool isClient)
    {
        // Keep the socket for sending and receiving
        _socket = socket;

        // Build the encryptor from the session key, with a 16-byte tag
        _aes = new AesGcm(sessionKey, TagSize);

        // "condition ? a : b" = a if the condition is true, otherwise b.
        // A client sends client-to-server messages and receives server-to-client ones; a server the opposite
        _sendDirection = isClient ? ClientToServer : ServerToClient;
        _receiveDirection = isClient ? ServerToClient : ClientToServer;
    }

    // Purpose: Runs the client's side of the handshake and creates the encrypted channel.
    // Input:   socket - an already connected socket; serverPublicKey - the server's RSA public key;
    //          stop - optional cancel signal.
    // Output:  a SecureChannel ready to send and receive. Throws if the server does not start the handshake correctly.
    // The client's side of the handshake.
    // socket = an already connected socket, serverPublicKey = the server's RSA public key,
    // stop = a signal that cancels the waiting ("= default" makes it optional)
    public static async Task<SecureChannel> ConnectAsync(Socket socket, RSA serverPublicKey, CancellationToken stop = default)
    {
        // 1. The server speaks first: 32 fresh random bytes
        byte[]? serverRandom = await MessageFraming.ReceiveFrameAsync(socket, stop);

        // null = the server closed the connection; a different length = this is not our protocol
        if (serverRandom is null || serverRandom.Length != RandomSize)
        {
            throw new IOException("The server did not start the handshake correctly.");
        }

        // 2. Choose a secret and send it encrypted with the server's public key.
        //    Only the holder of the matching private key - the real server - can read it.
        //    32 random bytes from a cryptographically secure source
        byte[] clientSecret = RandomNumberGenerator.GetBytes(RandomSize);

        // Encrypt the secret with RSA. OAEP-SHA256 = the safe padding scheme for RSA encryption
        byte[] encryptedSecret = serverPublicKey.Encrypt(clientSecret, RSAEncryptionPadding.OaepSHA256);

        // Send the encrypted secret to the server as one frame
        await MessageFraming.SendFrameAsync(socket, encryptedSecret, stop);

        // 3. Both sides compute the same session key
        return new SecureChannel(socket, DeriveSessionKey(clientSecret, serverRandom), isClient: true);
    }

    // Purpose: Runs the server's side of the handshake and creates the encrypted channel.
    // Input:   socket - the accepted client socket; serverPrivateKey - the server's RSA private key;
    //          stop - optional cancel signal.
    // Output:  a SecureChannel ready to send and receive, or null if the client hung up during the handshake.
    // The server's side of the handshake. Returns null if the client hung up before finishing it
    public static async Task<SecureChannel?> AcceptAsync(Socket socket, RSA serverPrivateKey, CancellationToken stop = default)
    {
        // 1. Send 32 fresh random bytes. Because they are new for every connection, the session key is
        //    new for every connection too - a recording of an old conversation is useless if sent again
        byte[] serverRandom = RandomNumberGenerator.GetBytes(RandomSize);
        await MessageFraming.SendFrameAsync(socket, serverRandom, stop);

        // 2. Receive the client's secret and open it with our private key
        byte[]? encryptedSecret = await MessageFraming.ReceiveFrameAsync(socket, stop);

        // null = the client closed the connection in the middle of the handshake
        if (encryptedSecret is null)
        {
            return null;
        }

        // Declared before the "lock" block so it can still be used after it
        byte[] clientSecret;
        lock (serverPrivateKey) // one private key object serves all the connections - one at a time
        {
            // Decrypt with the private key. Throws if the bytes were not encrypted with our public key
            clientSecret = serverPrivateKey.Decrypt(encryptedSecret, RSAEncryptionPadding.OaepSHA256);
        }

        // 3. Both sides compute the same session key
        return new SecureChannel(socket, DeriveSessionKey(clientSecret, serverRandom), isClient: false);
    }

    // Purpose: Computes the session key both sides share, from the two handshake values.
    // Input:   clientSecret - the client's 32 secret bytes; serverRandom - the server's 32 random bytes.
    // Output:  the 32-byte session key.
    // Session key = SHA-256(client secret + server random). SHA-256 always gives 32 bytes - exactly an AES-256 key
    private static byte[] DeriveSessionKey(byte[] clientSecret, byte[] serverRandom)
    {
        // One array big enough for both values, one after the other
        byte[] both = new byte[clientSecret.Length + serverRandom.Length];

        // The client secret goes first, starting at index 0
        clientSecret.CopyTo(both, 0);

        // The server random goes right after it
        serverRandom.CopyTo(both, clientSecret.Length);

        // Hash the 64 bytes into 32 - this is the session key
        return SHA256.HashData(both);
    }

    // Purpose: Encrypts one text message with AES-GCM and sends it as a frame.
    // Input:   text - the message to send; stop - optional cancel signal.
    // Output:  nothing (a Task that ends when the frame was sent).
    // Encrypts one text message and sends it
    public async Task SendAsync(string text, CancellationToken stop = default)
    {
        // Text -> bytes
        byte[] plain = Encoding.UTF8.GetBytes(text);

        // A nonce is a random value used for one message only. It makes the same text
        // encrypt to different bytes every time
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);

        // An empty array for the encrypted bytes. AES-GCM output is exactly as long as the input
        byte[] encrypted = new byte[plain.Length];

        // An empty array that Encrypt fills with the 16-byte tag
        byte[] tag = new byte[TagSize];

        // Encrypt. The tag is a fingerprint of the message that only a holder of the key can compute.
        // The label (direction + message number) is not sent, but it is mixed into the tag
        _aes.Encrypt(nonce, plain, encrypted, tag, BuildLabel(_sendDirection, _sendCounter));

        // The next message gets the next number
        _sendCounter++;

        // The frame: nonce | tag | encrypted text
        byte[] frame = new byte[NonceSize + TagSize + encrypted.Length];
        nonce.CopyTo(frame, 0);                          // bytes 0-11
        tag.CopyTo(frame, NonceSize);                    // bytes 12-27
        encrypted.CopyTo(frame, NonceSize + TagSize);    // byte 28 and on

        // MessageFraming adds the 4-byte length header and sends everything
        await MessageFraming.SendFrameAsync(_socket, frame, stop);
    }

    // Purpose: Receives the next frame, checks it and decrypts it back to text.
    // Input:   stop - optional cancel signal.
    // Output:  the decrypted text, or null if the other side closed the connection.
    //          Throws if the frame is too short or was changed, replayed or sent in the wrong direction.
    // Returns the next message, or null if the other side closed the connection
    public async Task<string?> ReceiveAsync(CancellationToken stop = default)
    {
        // Wait for one whole frame
        byte[]? frame = await MessageFraming.ReceiveFrameAsync(_socket, stop);

        // null = the other side closed the connection
        if (frame is null)
        {
            return null;
        }

        // A real frame holds at least a nonce and a tag (28 bytes)
        if (frame.Length < NonceSize + TagSize)
        {
            throw new InvalidDataException("The encrypted frame is too short.");
        }

        // Split the frame back into its three parts. "[a..b]" = a copy of the bytes from index a up to (not including) b
        byte[] nonce = frame[..NonceSize];                        // bytes 0-11
        byte[] tag = frame[NonceSize..(NonceSize + TagSize)];     // bytes 12-27
        byte[] encrypted = frame[(NonceSize + TagSize)..];        // byte 28 to the end

        // An empty array that Decrypt fills with the original bytes
        byte[] plain = new byte[encrypted.Length];

        // Decrypt checks the tag first. If even one bit was changed on the way, if the message is a
        // copy of an earlier one, or if it was meant for the other direction - it throws and nothing is read
        _aes.Decrypt(nonce, encrypted, tag, plain, BuildLabel(_receiveDirection, _receiveCounter));

        // The next message is expected with the next number
        _receiveCounter++;

        // Bytes -> text
        return Encoding.UTF8.GetString(plain);
    }

    // Purpose: Builds the extra label that is mixed into each message's tag.
    // Input:   direction - ClientToServer or ServerToClient; counter - the message number.
    // Output:  a 9-byte array: the direction byte, then the counter.
    // 9 bytes: the direction, then the message number. Both sides build it on their own and never send it
    private static byte[] BuildLabel(byte direction, long counter)
    {
        // 1 byte for the direction + 8 bytes for the counter (a long)
        byte[] label = new byte[9];

        // The first byte is the direction
        label[0] = direction;

        // AsSpan(1) = the label from index 1 on; the counter is written there as 8 bytes, big-endian
        BinaryPrimitives.WriteInt64BigEndian(label.AsSpan(1), counter);

        return label;
    }

    // Purpose: Releases the AES encryptor and wipes the session key from memory.
    // Input:   nothing.
    // Output:  nothing.
    // Called automatically at the end of a "using" block
    public void Dispose()
    {
        // Wipes the session key from memory. The socket itself is closed by whoever opened it
        _aes.Dispose();
    }
}
