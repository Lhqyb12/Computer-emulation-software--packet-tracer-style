namespace NetSim.Server.Dtos;

// DTO = Data Transfer Object. This class is deliberately "dumb" - no logic, just fields/properties for
// shipping data to the client. It's kept separate from User (the real DB model) on purpose: this way a
// sensitive field like PasswordHash can never be accidentally returned to the client - because this class
// simply doesn't contain it in the first place.
// This is the answer the server sends back for almost every message
public class AuthResponse
{
    // Plain bool: true = the operation (register/login/...) succeeded, false = it failed.
    // The client reads this field to decide whether to move on or to show the message as an error
    public bool Success { get; set; }

    // Human-readable message ("Wrong email or password", "Account created" etc.).
    // "= string.Empty" is a default value: since the project has <Nullable>enable</Nullable> (in the csproj),
    // a plain string (not string?) must always be non-null - without this default there would be a compiler
    // warning (or a NullReferenceException at runtime if someone forgets to initialize the property).
    // string.Empty rather than null - so code that uses the message (e.g. displaying it in the UI) never crashes
    public string Message { get; set; } = string.Empty;

    // "User" or "Admin". Filled only after a successful login - the client uses it to decide which screens to show
    public string Role { get; set; } = string.Empty;

    // The JWT. Filled only after a successful login - the client sends it back with every later request
    public string Token { get; set; } = string.Empty;

    // The user's email. Filled after a Google sign-in, where the client never typed an email itself
    public string Email { get; set; } = string.Empty;

    // True only when a login was refused because the email was never verified - tells the client to
    // open the "verify your email" screen instead of just showing an error
    public bool NeedsVerification { get; set; }



}
