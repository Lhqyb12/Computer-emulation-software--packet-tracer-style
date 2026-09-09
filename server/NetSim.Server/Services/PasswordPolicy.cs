namespace NetSim.Server.Services;

/// <summary>
/// Server-side rules for what counts as an acceptable password.
/// Returns an error message string, or <c>null</c> when the password is fine.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static string? Validate(string password, string username)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"Password must be at least {MinLength} characters long.";

        if (!password.Any(char.IsLetter))
            return "Password must contain at least one letter.";

        if (!password.Any(char.IsDigit))
            return "Password must contain at least one number.";

        if (password.All(char.IsLetterOrDigit))
            return "Password must contain at least one special character (for example ! ? # $).";

        // Reject a password that contains the username – it's weak and too easy to guess.
        // Skip very short usernames so we don't ban common letter sequences.
        var trimmedUser = username?.Trim() ?? string.Empty;
        if (trimmedUser.Length >= 3 &&
            password.Contains(trimmedUser, StringComparison.OrdinalIgnoreCase))
            return "Password is too weak — it must not contain your username.";

        return null; // all rules passed
    }
}
