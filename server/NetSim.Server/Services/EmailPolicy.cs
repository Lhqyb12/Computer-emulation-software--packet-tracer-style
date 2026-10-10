using System.Net.Mail;

namespace NetSim.Server.Services;

/// <summary>
/// Server-side rule for what counts as an acceptable email address.
/// </summary>
// static class - no state, just a pure validation function (the same idea as PasswordPolicy)
public static class EmailPolicy
{
    // The longest address the email standard allows - anything longer is junk, not a typo
    public const int MaxLength = 254;

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaxLength)
            return false;

        // MailAddress is .NET's own email parser - it knows the real rules (exactly one @, something
        // on both sides of it, no illegal characters), so we don't hand-write a fragile regex for them
        if (!MailAddress.TryCreate(email, out MailAddress? parsed))
            return false;

        // The parser also accepts "Lea <lea@example.com>" (a display name around the address).
        // Comparing back to the input makes sure we got the bare address and nothing else
        if (parsed.Address != email)
            return false;

        // The part after the @ must look like a real domain: "gmail.com" has a dot, "b" does not
        string host = parsed.Host;
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.') && !host.Contains("..");
    }
}
