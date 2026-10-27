namespace NetSim.Application.State;

// The simple implementation of ICurrentUser: it only stores the email it was given
public sealed class CurrentUser : ICurrentUser
{
    // "private set" = only this class can change it, and only through SignIn
    public string Email { get; private set; } = string.Empty;

    // Purpose: Remembers who signed in.
    // Input:   email - the email the server returned at login.
    // Output:  nothing.
    public void SignIn(string email) => Email = email;
}
