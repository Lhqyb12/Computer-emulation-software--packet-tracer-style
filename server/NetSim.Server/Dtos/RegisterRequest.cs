namespace NetSim.Server.Dtos;

// DTO for a register request - like LoginRequest, but with an extra Username because registration creates a
// new user and needs a display name, not just a login identity
public class RegisterRequest
{
    // The user's display name - also checked inside PasswordPolicy.Validate (to make sure the password doesn't contain it)
    public string Username { get; set; } = string.Empty;

    // The email will later be used to log in (LoginRequest.Email) - must be unique, checked in
    // AuthService.RegisterAsync against the table before creating the user (AnyAsync)
    public string Email { get; set; } = string.Empty;

    // Plain-text password - like in LoginRequest, lives in memory only briefly and never reaches the DB as-is;
    // AuthService.RegisterAsync first runs it through PasswordPolicy.Validate and then through PasswordHasher.Hash
    public string Password { get; set; } = string.Empty;
}
