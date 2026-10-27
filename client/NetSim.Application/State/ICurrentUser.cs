namespace NetSim.Application.State;

// Who is signed in right now, for the services that need to know it (for example: "the classes I
// teach"). Registered as Scoped: MainWindowViewModel opens a new scope on every sign-in and calls
// SignIn once, so every service in that scope sees the same user
public interface ICurrentUser
{
    // The signed-in user's email. Empty until SignIn is called
    string Email { get; }

    // Purpose: Remembers who signed in.
    // Input:   email - the email the server returned at login.
    // Output:  nothing.
    void SignIn(string email);
}
