namespace NetSim.Server.Dtos;

// DTO for a login request - exactly the two fields the client sends in the body of POST /api/auth/login (as JSON),
// which [ApiController] automatically turns into a C# object via model binding
public class LoginRequest
{
    // Email is used as the login identity - not Username, because an email is inherently unique
    // and easier for the user to remember/recover (password reset etc.) than a username
    public string Email { get; set; } = string.Empty;

    // The password arrives here as plain text - and that's fine! It exists like this only briefly, in memory,
    // for the duration of the request, and is encrypted in transit by HTTPS (UseHttpsRedirection in Program.cs).
    // On the server side it is never stored as-is - it's compared against the stored hash via
    // PasswordHasher.Verify and then discarded
    public string Password { get; set; } = string.Empty;
}
