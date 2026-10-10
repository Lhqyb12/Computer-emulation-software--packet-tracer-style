using System.Security.Cryptography;

namespace NetSim.Server.Networking;

// The server's RSA key pair - its identity. Created once, on the first run, and kept in the Keys folder:
//   server-private.pem - stays on the server and must never be shared or committed to git
//   server-public.pem  - copied into the client, so the client can recognise the real server
public class ServerKeys
{
    public RSA PrivateKey { get; }

    public ServerKeys(IHostEnvironment environment, ILogger<ServerKeys> logger)
    {
        string folder = Path.Combine(environment.ContentRootPath, "Keys");
        string privatePath = Path.Combine(folder, "server-private.pem");
        string publicPath = Path.Combine(folder, "server-public.pem");

        if (File.Exists(privatePath))
        {
            // Every later run: load the same key, so clients that know our public key still recognise us
            PrivateKey = RSA.Create();
            PrivateKey.ImportFromPem(File.ReadAllText(privatePath));
        }
        else
        {
            // First run: create a new 2048-bit key pair and save both halves as text (PEM) files
            PrivateKey = RSA.Create(2048);
            Directory.CreateDirectory(folder);
            File.WriteAllText(privatePath, PrivateKey.ExportRSAPrivateKeyPem());
            File.WriteAllText(publicPath, PrivateKey.ExportSubjectPublicKeyInfoPem());
            logger.LogWarning("A new server key pair was created in {Folder}. Copy server-public.pem into the client", folder);
        }
    }
}
