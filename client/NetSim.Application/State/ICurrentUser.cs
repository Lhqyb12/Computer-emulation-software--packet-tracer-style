namespace NetSim.Application.State;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Who is signed in right now, for the services that need to know it (for example: "the classes I
// teach"). Registered as Scoped: MainWindowViewModel opens a new scope on every sign-in and calls
// SignIn once, so every service in that scope sees the same user
public interface ICurrentUser
{
    // The signed-in user's email. Empty until SignIn is called
    string Email { get; }

    
    // The JWT the server gave at login - sent with every request that needs the user's identity. Empty until SignIn is called
    string Token { get; }


       // Purpose: Remembers who signed in.
    // Input:   email and token - the email and the JWT the server returned at login.
    // Output:  nothing.
    void SignIn(string email, string token);

}
