using System.Security.Cryptography;  // RSA

namespace NetSim.Server.Networking;

// The server's RSA key pair - its identity. Created once, on the first run, and kept in the Keys folder:
//   server-private.pem - stays on the server and must never be shared or committed to git
//   server-public.pem  - copied into the client, so the client can recognise the real server
public class ServerKeys
{
    // The private key, used by SecureChannel in every handshake.
    // "{ get; }" = it can be read from outside, and set only inside the constructor
    public RSA PrivateKey { get; }

    // Purpose: Loads the server's RSA key pair from the Keys folder, or creates and saves a new one on the first run.
    // Input:   environment - where the server runs (for its folder); logger - writes to the log.
    // Output:  none (a constructor) - a ServerKeys object whose PrivateKey is ready.
    // The constructor. Dependency injection passes in:
    // environment = information about where the server runs (we need its folder), logger = writes to the log
    public ServerKeys(IHostEnvironment environment, ILogger<ServerKeys> logger)
    {
        // The Keys folder inside the server's project folder. Path.Combine joins the parts with "\"
        string folder = Path.Combine(environment.ContentRootPath, "Keys");

        // The full paths of the two key files
        string privatePath = Path.Combine(folder, "server-private.pem");
        string publicPath = Path.Combine(folder, "server-public.pem");

        // Is there already a saved private key?
        if (File.Exists(privatePath))
        {
            // Every later run: load the same key, so clients that know our public key still recognise us.
            // Create an empty RSA object...
            PrivateKey = RSA.Create();

            // ...and fill it with the key that is written in the file
            PrivateKey.ImportFromPem(File.ReadAllText(privatePath));
        }
        // No key file yet
        else
        {
            // First run: create a new 2048-bit key pair and save both halves as text (PEM) files
            PrivateKey = RSA.Create(2048);

            // Create the Keys folder if it does not exist yet
            Directory.CreateDirectory(folder);

            // Write the private half to its file
            File.WriteAllText(privatePath, PrivateKey.ExportRSAPrivateKeyPem());

            // Write the public half to its file
            File.WriteAllText(publicPath, PrivateKey.ExportSubjectPublicKeyInfoPem());

            // A warning, because the client has to be updated with the new public key
            logger.LogWarning("A new server key pair was created in {Folder}. Copy server-public.pem into the client", folder);
        }
    }
}
