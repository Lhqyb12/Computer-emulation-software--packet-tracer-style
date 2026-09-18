namespace NetSim.Server.Dtos;

// DTO = Data Transfer Object. This class is deliberately "dumb" - no logic, just fields/properties for
// shipping data over the API. It's kept separate from User (the real DB model) on purpose: this way a
// sensitive field like PasswordHash can never be accidentally returned to the client - because this class
// simply doesn't contain it in the first place
public class AuthResponse
{
    // Plain bool: true = the operation (register/login) succeeded, false = it failed.
    // The controller (AuthController) reads this field to decide whether to return 200/400/401
    public bool Success { get; set; }

    // Human-readable message ("Wrong email or password", "Account created" etc.).
    // "= string.Empty" is a default value: since the project has <Nullable>enable</Nullable> (in the csproj),
    // a plain string (not string?) must always be non-null - without this default there would be a compiler
    // warning (or a NullReferenceException at runtime if someone forgets to initialize the property).
    // string.Empty rather than null - so code that uses the message (e.g. displaying it in the UI) never crashes
    public string Message { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
