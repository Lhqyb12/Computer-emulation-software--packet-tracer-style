namespace NetSim.App.Services;

// The server's public key, copied from the server's Keys/server-public.pem file.
// The client uses it to make sure it is talking to the real server: only the holder of the matching
// private key can open what we encrypt with this key. A public key is not a secret, so it may live in the code
public static class ServerPublicKey
{
    public const string Pem = """
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAw34Rgm8pd7SKFss1p4SJ
/VhJ1rQtu1dpUIp6/P0w7ViUYEM41rhX6/5txuvuDFeM0foUge4YRPzubiXc2SK7
MGNTbQV9i7vGuRB8SDr898FVuxsxrIIeKJtziiw4//lYnkxNEuKuvuQGykPQiN4x
YXEKN0RglFF98svVUpNY0jRyr1XlQxwMiHDtC2eUU6DDSOaGkH9cRrs1lt4C8TVE
NLAxG2/HVFstK1GwKM5ygo9Nvkv0FwfHre8psNsfQeUiXG3lSS0LqeXf+ItTnsLF
eTCd0hThia63gjQslSSb/oohfLt7uBQQUarE7e3jIsBBaBqr1/HAmXQ5RjsTJjFn
PQIDAQAB
-----END PUBLIC KEY-----
""";
}
