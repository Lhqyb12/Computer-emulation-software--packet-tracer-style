namespace NetSim.Server.Models;

// One row in the SecurityEvents table: something security-related that happened on the server and that the
// owner should be able to look back at - a failed login, a password reset, a changed password...
// This is an "audit log". Unlike the Logs tab (kept only in memory, gone when the server stops), these
// rows are saved in the database, so the history survives restarts.
//
// What is deliberately never stored here: passwords, reset codes, tokens - not even wrong ones.
// A wrong password is very often the right password with one typo
public class SecurityEvent
{
    // Primary key, auto-incremented by the database (same EF Core convention as User.Id)
    public int Id { get; set; }

    // When it happened - UTC, like every other timestamp in the database
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // A short fixed name of what happened, e.g. "Login failed" - the EVENT column of the dashboard
    public string EventType { get; set; } = string.Empty;

    // The email the event is about, exactly as the client sent it. Nullable: some events have none.
    // Note it is NOT a foreign key to Users - a failed login can be for an email that isn't registered at all
    public string? Email { get; set; }

    // The network address the request came from. Nullable: unknown if there was no HTTP request
    public string? IpAddress { get; set; }

    // The specific reason, for the owner's eyes only (e.g. "Wrong password" vs "Unknown email") -
    // exactly the detail the client is NOT told
    public string Details { get; set; } = string.Empty;
}
