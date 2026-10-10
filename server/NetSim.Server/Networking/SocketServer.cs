using System.Net;
using System.Net.Sockets;
using System.Text;


namespace NetSim.Server.Networking;

// Our own TCP server, written directly on the Socket class - no ASP.NET, no TcpListener.
// Stage 1: it only listens on a port and accepts connections
public class SocketServer
{
    // The port our server listens on. Kestrel (HTTPS) still uses 7089, so we need a different one
    public const int Port = 9000;

    private readonly ILogger<SocketServer> _logger; //print tool
    private readonly MessageRouter _router;
    private readonly ServerKeys _keys;

    public SocketServer(ILogger<SocketServer> logger, MessageRouter router, ServerKeys keys)
    {
        _logger = logger;
        _router = router;
        _keys = keys;
    }


    public async Task RunAsync(CancellationToken stop)
    {
        try
        {
            // InterNetwork = IPv4, Stream + Tcp = a TCP socket (reliable, ordered stream of bytes)
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind: take port 9000 on this computer. Loopback = 127.0.0.1, only this computer can connect
            listener.Bind(new IPEndPoint(IPAddress.Loopback, Port));

            // Listen: from now on the operating system accepts TCP connections on this port for us
            listener.Listen();
            _logger.LogInformation("Socket server listening on port {Port}", Port);
            


            while (true)
            {
                // Accept: wait here until a client connects. The result is a NEW socket that
                // represents the connection with this one client; "listener" keeps listening
                Socket client = await listener.AcceptAsync(stop);
                _logger.LogInformation("Client connected from {Address}", client.RemoteEndPoint);

                // Handle this client in the background and go straight back to waiting for the next one.
                // "_ =" means: start it and do not wait for it to finish
                _ = HandleClientAsync(client, stop);

            }
        }
        catch (OperationCanceledException)
        {
            // The server is shutting down - this is the normal way out of the loop
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Socket server stopped because of an error");
        }

    }
    
        // The whole conversation with one client: handshake, then read a message, answer it, and wait
    // for the next one, until the client disconnects
    private async Task HandleClientAsync(Socket client, CancellationToken stop)
    {
        try
        {
            // "using" closes the socket when we leave this block, whatever happens inside
            using (client)
            {
                // The handshake comes first: agree on a secret key for this connection.
                // Nothing else is accepted from a client before it
                using SecureChannel? channel = await SecureChannel.AcceptAsync(client, _keys.PrivateKey, stop);
                if (channel is null)
                {
                    _logger.LogInformation("Client disconnected before the handshake finished");
                    return;
                }

                _logger.LogInformation("Secure channel established with {Address}", client.RemoteEndPoint);
                
                // The client's address, without the port - written next to every request and security event
                string? clientIp = (client.RemoteEndPoint as IPEndPoint)?.Address.ToString();


                while (true)
                {
                    // One whole message, already decrypted and checked. null = the client hung up
                    string? text = await channel.ReceiveAsync(stop);
                    if (text is null)
                    {
                        _logger.LogInformation("Client disconnected");
                        return;
                    }

                    // The router understands the message, runs the action and returns the answer
                    string reply = await _router.HandleAsync(text, clientIp);


                    // The answer goes back encrypted, under the same session key
                    await channel.SendAsync(reply, stop);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The server is shutting down
        }
        catch (Exception ex)
        {
            // One broken connection must not take the whole server down
            _logger.LogWarning(ex, "The connection with a client failed");
        }
    }



}
