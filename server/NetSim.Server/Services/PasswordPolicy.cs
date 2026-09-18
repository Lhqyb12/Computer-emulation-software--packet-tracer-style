namespace NetSim.Server.Services;

/// <summary>
/// Server-side rules for what counts as an acceptable password.
/// Returns an error message string, or <c>null</c> when the password is fine.
/// </summary>
// static class - again, no state, just a pure validation function
public static class PasswordPolicy
{
    // const int (not a magic number scattered through the code) - so the number 8 appears exactly once,
    // and so it can also be reused inside the error message itself ($"...{MinLength}...") without duplicating the number
    public const int MinLength = 8;

    // Choosing string? (nullable string) as the return type is the central design decision of this function:
    // null = "no error, the password is fine", a non-null string = the error message to display.
    // This is a lighter alternative to throwing an Exception - because a weak password is an *expected and
    // common* user-input scenario, not an exceptional failure - and throwing/catching exceptions for normal
    // control flow is both slower and less readable
    public static string? Validate(string password, string username)
    {
        // Check #1: minimum length. IsNullOrEmpty comes first - guards against a NullReferenceException if
        // password.Length ran on null (even though Nullable being enabled should prevent this in most cases,
        // this is extra protection at the input boundary)
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"Password must be at least {MinLength} characters long.";

        // Check #2: at least one letter. Any(predicate) scans every character and checks if at least one
        // satisfies the condition - char.IsLetter is passed directly as a method group (as a Func<char,bool>),
        // and it's Unicode-aware (Hebrew letters or other foreign letters also count as "a letter")
        if (!password.Any(char.IsLetter))
            return "Password must contain at least one letter.";

        // Check #3: at least one digit - same idea with char.IsDigit
        if (!password.Any(char.IsDigit))
            return "Password must contain at least one number.";

        // Check #4: at least one special character - checked via a clever negation: if *every* character is a
        // letter-or-digit (All), that means there's no special character at all. This is the inverse of writing
        // an explicit list of "allowed special characters" (a whitelist) - it saves maintaining such a list, but
        // implicitly "allows" any character that isn't a letter/digit (including, say, a space or rare Unicode characters)
        if (password.All(char.IsLetterOrDigit))
            return "Password must contain at least one special character (for example ! ? # $).";

        // Reject a password that contains the username – it's weak and too easy to guess.
        // Skip very short usernames so we don't ban common letter sequences.
        // Trim() removes leading/trailing whitespace; "?? string.Empty" handles the case where username itself
        // is null, so the Contains comparison below doesn't fail/throw
        var trimmedUser = username?.Trim() ?? string.Empty;
        // The "trimmedUser.Length >= 3" condition is a deliberate security/usability tradeoff: a very short
        // username (e.g. "al") could easily appear by coincidence in almost any strong password, and this rule
        // would then wrongly reject perfectly good passwords
        if (trimmedUser.Length >= 3 &&
            // StringComparison.OrdinalIgnoreCase is chosen deliberately over
            // StringComparison.CurrentCultureIgnoreCase: a "culture-aware" comparison can behave differently
            // across servers/locale settings (e.g. the well-known "Turkish I problem"), while Ordinal simply
            // compares byte/code-point values - 100% consistent in any environment
            password.Contains(trimmedUser, StringComparison.OrdinalIgnoreCase))
            return "Password is too weak — it must not contain your username.";

        // All checks passed - null signals "valid". Note: the checks run in a fixed order and stop (return) at
        // the first one that fails - this way the user gets one clear message about the first problem, instead
        // of a confusing list of every issue at once
        return null; // all rules passed
    }
}
