namespace NetSim.Server.Models;

// This is the real Entity mapped to the Users table in Postgres by EF Core (unlike the DTOs, which only
// "travel" over the API). Notice there's no explicit [Table]/[Column] attribute here - EF Core relies on
// conventions: class name "User" -> table name "Users" (plural, as also defined in AppDbContext.Users)
public class User
{
    // "Id" is a special name by EF Core convention: a property named Id (or "<ClassName>Id") is automatically
    // recognized as the Primary Key. Since its type is int, EF Core also configures it as IDENTITY
    // (auto-increment) in the database - every new user automatically gets the next number, no need to
    // generate the Id in code
    public int Id { get; set; }

    // The user's display name - a plain text field, also used in the PasswordPolicy check (that the password doesn't contain it)
    public string Username { get; set; } = string.Empty;

    // The email is effectively a unique business "key" - the uniqueness is actually enforced at the code level
    // (the check in AuthService.RegisterAsync before adding the user), not as a database constraint right now
    public string Email { get; set; } = string.Empty;

    // The actual password is *never* stored here - only the string returned by PasswordHasher.Hash
    // (in the "iterations.salt.hash" format, see PasswordHasher.cs). The name "PasswordHash" deliberately makes
    // it clear when reading the code/schema that this is not the password itself, to prevent someone from
    // ever mistakenly trying to "display" it later
    public string PasswordHash { get; set; } = string.Empty;

    // "= DateTime.UtcNow" as a default: the value is set the moment the C# object is created (in memory), not
    // the moment it's actually saved to the DB - in practice the difference is negligible (a few milliseconds
    // between new User{} and SaveChangesAsync).
    // UtcNow (not Now) is chosen deliberately: servers can run in different time zones - storing in UTC prevents
    // confusion/bugs when comparing timestamps created in different environments (e.g. a server in Israel vs a
    // cloud server in the US)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; } 
    public string Role { get; set; } = "User";
    public string? ResetCodeHash { get; set; }
    public DateTime? ResetCodeExpiresAt { get; set; }
    public int ResetAttempts { get; set; } //number of attempts for writing the code that the mails sends for reset
    public string? GoogleId { get; set; }



}
