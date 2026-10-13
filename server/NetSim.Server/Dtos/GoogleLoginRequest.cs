namespace NetSim.Server.Dtos;

// The data of a "google-login" message
public class GoogleLoginRequest
{
    // The "ID token" the client received from Google after the user signed in there. It is a text that
    // Google signed, and it states who the user is; the server checks the signature before trusting it
    public string IdToken { get; set; } = string.Empty;
}
