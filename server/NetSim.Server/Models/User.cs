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

    // The email is effectively a unique business "key" - the uniqueness is enforced twice: at the code level
    // (the check in AuthService.RegisterAsync before adding the user) and by a unique index in the database
    // itself (see AppDbContext.OnModelCreating)
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

    // When the user last signed in. "DateTime?" = a date or null; null = never signed in yet
    public DateTime? LastLoginAt { get; set; }

    // "User" or "Admin". Every new account starts as "User"; an admin can open the Users screen
    public string Role { get; set; } = "User";

    // The hash of the password-reset code that was emailed (never the code itself). null = no active reset request
    public string? ResetCodeHash { get; set; }

    // When that reset code stops being valid (15 minutes after it was sent)
    public DateTime? ResetCodeExpiresAt { get; set; }

    // How many wrong reset codes were typed so far. At 5 the code is cancelled
    public int ResetAttempts { get; set; }

    // Google's permanent id for this person. null = the account was never used with Google sign-in
    public string? GoogleId { get; set; }

    // False until the user proves they own the email address, by typing the code we emailed them
    public bool EmailVerified { get; set; }

    // Same three fields as the password-reset code above, but for the email-verification code:
    // the hash of the code, when it expires, and how many wrong codes were typed
    public string? VerifyCodeHash { get; set; }
    public DateTime? VerifyCodeExpiresAt { get; set; }
    public int VerifyAttempts { get; set; }




}
