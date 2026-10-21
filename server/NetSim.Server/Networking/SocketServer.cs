using System.Net;          // IPAddress and IPEndPoint (an address + a port)
using System.Net.Sockets;  // The Socket class and its settings (AddressFamily, SocketType, ProtocolType)
using System.Text;         // Text encodings. Nothing in this file uses it right now


// The "folder" this class belongs to. Other files reach it with "using NetSim.Server.Networking"
namespace NetSim.Server.Networking;

// Our own TCP server, written directly on the Socket class - no ASP.NET, no TcpListener.
// It listens on a port, accepts every client that connects, sets up an encrypted channel with it,
// and then answers that client's messages one after another until it disconnects
public class SocketServer
{
    // The port our server listens on. "public const" = a fixed value that other classes can read too
    public const int Port = 9000;

    // The logger: writes lines to the server's log (the console) so we can see what is happening.
    // "readonly" = set once in the constructor and never replaced afterwards
    private readonly ILogger<SocketServer> _logger;

    // The router: gets the text of a message, decides which action it asks for, and returns the answer
    private readonly MessageRouter _router;

    // Holds the server's RSA private key, needed for the handshake with every client
    private readonly ServerKeys _keys;

    // Purpose: Builds the socket server with the objects it needs.
    // Input:   logger - writes to the log; router - answers messages; keys - holds the RSA private key.
    // Output:  none (a constructor) - a new SocketServer.
    // The constructor. We do not create these three objects ourselves; dependency injection
    // creates them and passes them in when it builds the SocketServer
    public SocketServer(ILogger<SocketServer> logger, MessageRouter router, ServerKeys keys)
    {
        // Keep the three objects in the fields so the other methods can use them
        _logger = logger;
        _router = router;
        _keys = keys;
    }


    // Purpose: Opens the listening socket on port 9000 and accepts clients until the server shuts down.
    // Input:   stop - the signal that is raised when the server shuts down.
    // Output:  nothing (a Task that ends when the server stops or fails).
    // The main loop of the server: open the listening socket and accept clients forever.
    // stop = a signal that is raised when the server shuts down, and ends the waiting
    public async Task RunAsync(CancellationToken stop)
    {
        // Everything runs inside "try" so that an error is caught below instead of crashing the program
        try
        {
            // InterNetwork = IPv4, Stream + Tcp = a TCP socket (reliable, ordered stream of bytes).
            // "using var" = the socket is closed automatically when we leave this block
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind: take port 9000 on this computer. Loopback = 127.0.0.1, only this computer can connect
            listener.Bind(new IPEndPoint(IPAddress.Loopback, Port));

            // Listen: from now on the operating system accepts TCP connections on this port for us
            listener.Listen();

            // Write to the log that the server is up. {Port} is replaced by the value of Port
            _logger.LogInformation("Socket server listening on port {Port}", Port);



            // An endless loop: one round for every client that connects.
            // The only ways out are the two "catch" blocks below
            while (true)
            {
                // Accept: wait here until a client connects. The result is a NEW socket that
                // represents the connection with this one client; "listener" keeps listening
                Socket client = await listener.AcceptAsync(stop);

                // Write to the log who connected. RemoteEndPoint = the client's IP address and port
                _logger.LogInformation("Client connected from {Address}", client.RemoteEndPoint);

                // Handle this client in the background and go straight back to waiting for the next one.
                // "_ =" means: start it and do not wait for it to finish
                _ = HandleClientAsync(client, stop);

            }
        }
        // Thrown by AcceptAsync when the "stop" signal is raised
        catch (OperationCanceledException)
        {
            // The server is shutting down - this is the normal way out of the loop
        }
        // Any other error (for example, port 9000 is already taken by another program)
        catch (Exception ex)
        {
            // Write the error, with its details, to the log
            _logger.LogError(ex, "Socket server stopped because of an error");
        }

    }

    // Purpose: Runs the whole conversation with one client: the handshake, then a request/answer loop.
    // Input:   client - the socket of this client's connection; stop - the shutdown signal.
    // Output:  nothing (a Task that ends when the client disconnects or the connection fails).
    // The whole conversation with one client: handshake, then read a message, answer it, and wait
    // for the next one, until the client disconnects
    private async Task HandleClientAsync(Socket client, CancellationToken stop)
    {
        // An error with this client is caught below, so it cannot hurt the other clients
        try
        {
            // "using" closes the socket when we leave this block, whatever happens inside
            using (client)
            {
                // The handshake comes first: agree on a secret key for this connection.
                // Nothing else is accepted from a client before it.
                // "SecureChannel?" = the result is a channel, or null if the client left in the middle
                using SecureChannel? channel = await SecureChannel.AcceptAsync(client, _keys.PrivateKey, stop);

                // null = no channel was created
                if (channel is null)
                {
                    // Write it to the log and finish with this client
                    _logger.LogInformation("Client disconnected before the handshake finished");
                    return;
                }

                // The handshake succeeded: from here on everything is encrypted
                _logger.LogInformation("Secure channel established with {Address}", client.RemoteEndPoint);

                // The client's address, without the port - written next to every request and security event.
                // "as IPEndPoint" converts the general endpoint to an IP one (or null if it is not one),
                // and "?." continues only if it is not null, so clientIp can end up null
                string? clientIp = (client.RemoteEndPoint as IPEndPoint)?.Address.ToString();


                // An endless loop: one round for every message this client sends
                while (true)
                {
                    // One whole message, already decrypted and checked. null = the client hung up
                    string? text = await channel.ReceiveAsync(stop);

                    // The client closed the connection
                    if (text is null)
                    {
                        // Write it to the log and leave the method; the "using" lines close everything
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
        // Thrown by the waiting calls above when the "stop" signal is raised
        catch (OperationCanceledException)
        {
            // The server is shutting down
        }
        // Any other error: a broken connection, a message that failed its check, and so on
        catch (Exception ex)
        {
            // One broken connection must not take the whole server down.
            // Warning (not Error) because the server itself keeps running
            _logger.LogWarning(ex, "The connection with a client failed");
        }
    }



}
