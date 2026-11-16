namespace NetSim.App.Services;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The server's public key, copied from the server's Keys/server-public.pem file.
// The client uses it to make sure it is talking to the real server: only the holder of the matching
// private key can open what we encrypt with this key. A public key is not a secret, so it may live in the code
public static class ServerPublicKey
{
    // The key as PEM text: the key's bytes written in Base64, between a BEGIN line and an END line.
    // The three quote marks open a "raw string" - text that may span several lines exactly as it is written
    public const string Pem = """
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2bfabLwciHp9TIalDXod
Ofst/OoxbMLkIpOn20r+9VwBVIGEbgUM6LdZ4TLrVoT56cxMdGWLfxZBla2p4fdP
9TKrEACcv882kFUvqoeY1sdnXBMWfpD6qena4h8wrYik2q++ow+LKkfS4XDuUalF
2DCNNzoZlLts7oWfYS6R9SltwiNPZV43L7s27VsrLF7BPqXABrzao7771Q5/N0xO
KCuCPBjd02MvGV8RRDR5EfB8IkqVvPw80lpHqbRAElM0UNS3qNrJmRJ4J4KJfx7R
a/pwytSUjps/10x3cVGY+BuRItTtoH3BR+4TZwyNg0Jqbm4b9IKMsrWyXsBkymyc
tQIDAQAB
-----END PUBLIC KEY-----
""";
}
