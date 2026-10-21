using System.Net.Mail;  // MailAddress - .NET's own email address parser

namespace NetSim.Server.Services;

/// <summary>
/// Server-side rule for what counts as an acceptable email address.
/// </summary>
// static class - no state, just a pure validation function (the same idea as PasswordPolicy)
public static class EmailPolicy
{
    // The longest address the email standard allows - anything longer is junk, not a typo
    public const int MaxLength = 254;

    // Purpose: Checks whether a text is an acceptable email address (length, format, domain with a dot).
    // Input:   email - the text to check (may be null).
    // Output:  true if it is a valid email address, otherwise false.
    // Returns true if the text is an acceptable email address, false otherwise
    public static bool IsValid(string? email)
    {
        // Nothing was typed (null, empty or only spaces), or the text is too long
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaxLength)
            return false;

        // MailAddress is .NET's own email parser - it knows the real rules (exactly one @, something
        // on both sides of it, no illegal characters), so we don't hand-write a fragile regex for them.
        // TryCreate returns false instead of throwing; on success "out parsed" receives the parsed address
        if (!MailAddress.TryCreate(email, out MailAddress? parsed))
            return false;

        // The parser also accepts "Lea <lea@example.com>" (a display name around the address).
        // Comparing back to the input makes sure we got the bare address and nothing else
        if (parsed.Address != email)
            return false;

        // The part after the @ must look like a real domain: "gmail.com" has a dot, "b" does not
        string host = parsed.Host;

        // It has a dot, does not start or end with one, and has no two dots in a row
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.') && !host.Contains("..");
    }
}
