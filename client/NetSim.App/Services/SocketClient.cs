using System.IO;                     // IOException
using System.Net;                    // IPAddress and IPEndPoint (an address + a port)
using System.Net.Sockets;            // The Socket class and its settings
using System.Security.Cryptography;  // RSA
using System.Text;                   // Text encodings. Nothing in this file uses it right now
using System.Threading.Tasks;        // Task, async/await

// The "folder" this class belongs to. Other files reach it with "using NetSim.App.Services"
namespace NetSim.App.Services;

// The client side of our own TCP communication, written directly on the Socket class.
// Every request opens a new connection: connect, do the handshake that sets up the encrypted
// channel, send one text, read one text back, and close
public class SocketClient
{
    // Must match SocketServer.Port on the server. "const" = a fixed value that never changes
    private const int Port = 9000;

    // Sends one request to the server and returns the server's answer.
    // text = the request, as text. "async Task<string>" = the method can pause at an "await"
    // without freezing the window, and in the end it gives back a string
    public async Task<string> SendTextAsync(string text)
    {
        // Same three settings as the server: IPv4, stream, TCP.
        // "using var" = the socket is closed automatically when the method ends
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Connect: the TCP three-way handshake with the server happens here.
        // Loopback = 127.0.0.1 = the server runs on this same computer
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port));

        // An empty RSA object. RSA is asymmetric encryption: a public key encrypts,
        // and only the matching private key (which stays on the server) can decrypt
        using RSA serverKey = RSA.Create();

        // Load the server's public key into it. The key is built into the client as PEM text
        // (see ServerPublicKey.cs)
        serverKey.ImportFromPem(ServerPublicKey.Pem);

        // The handshake: agree with the server on a secret key for this connection.
        // The result is a channel that encrypts everything sent and decrypts everything received
        using SecureChannel channel = await SecureChannel.ConnectAsync(socket, serverKey);

        // From here on everything is encrypted: send the request
        await channel.SendAsync(text);

        // Wait for the server's answer. "string?" = the answer, or null if the server closed the connection
        string? reply = await channel.ReceiveAsync();

        // "??" = if reply is not null return it, otherwise throw an error that the caller will catch
        return reply ?? throw new IOException("The server closed the connection without answering.");


    }
}
