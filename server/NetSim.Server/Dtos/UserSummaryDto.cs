namespace NetSim.Server.Dtos;

// A DTO that describes one "row" in the user management screen - deliberately without PasswordHash,
// for exactly the same reason as in AuthResponse: there is no need for that sensitive data to leave the server at all
public class UserSummaryDto
{
    // The user's id in the database (needed so the admin can ask to delete this user)
    public int Id { get; set; }

    // The display name
    public string Username { get; set; } = string.Empty;

    // The email address
    public string Email { get; set; } = string.Empty;

    // When the account was created (UTC)
    public DateTime CreatedAt { get; set; }

    // When the user last signed in (UTC). "DateTime?" = null if they never signed in
    public DateTime? LastLoginAt { get; set; }

    // "User" or "Admin"
    public string Role { get; set; } = string.Empty;
}
