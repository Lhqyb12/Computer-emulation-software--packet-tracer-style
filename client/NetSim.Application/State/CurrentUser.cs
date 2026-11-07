namespace NetSim.Application.State;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The simple implementation of ICurrentUser: it only stores the email it was given
public sealed class CurrentUser : ICurrentUser
{
    // "private set" = only this class can change it, and only through SignIn
    public string Email { get; private set; } = string.Empty;

    
    // The JWT. The same "private set": only SignIn changes it
    public string Token { get; private set; } = string.Empty;


    // Purpose: Remembers who signed in.
    // Input:   email and token - the email and the JWT the server returned at login.
    // Output:  nothing.
    public void SignIn(string email, string token)
    {
        Email = email;   // remember the email
        Token = token;   // remember the token
    }

}
