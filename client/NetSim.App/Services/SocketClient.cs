using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NetSim.App.Services;

// The client side of our own TCP communication, written directly on the Socket class.
// Stage 2: connect, send one text, read one text back
public class SocketClient
{
    // Must match SocketServer.Port on the server
    private const int Port = 9000;

    public async Task<string> SendTextAsync(string text)
    {
        // Same three settings as the server: IPv4, stream, TCP
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Connect: the TCP three-way handshake with the server happens here.
        // Loopback = 127.0.0.1 = the server runs on this same computer
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port));

        // The server's public key, built into the client (see ServerPublicKey.cs)
        using RSA serverKey = RSA.Create();
        serverKey.ImportFromPem(ServerPublicKey.Pem);

        // The handshake: agree with the server on a secret key for this connection
        using SecureChannel channel = await SecureChannel.ConnectAsync(socket, serverKey);

        // From here on everything is encrypted: send the request, read the answer
        await channel.SendAsync(text);

        string? reply = await channel.ReceiveAsync();
        return reply ?? throw new IOException("The server closed the connection without answering.");


    }
}
