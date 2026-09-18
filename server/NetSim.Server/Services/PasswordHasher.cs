using System.Security.Cryptography;  // RandomNumberGenerator, Rfc2898DeriveBytes (PBKDF2), CryptographicOperations - all the cryptographic primitives

namespace NetSim.Server.Services;

// static class: there is no state to keep between calls here - every function is a pure function
// (input -> output, no side effects), so there's no reason to allow creating an instance (new PasswordHasher()) at all
public static class PasswordHasher
{
    // 16 bytes = 128 bits - a standard, widely accepted salt size for cryptography (makes the chance of two
    // identical salts completely negligible)
    private const int SaltSize = 16;
    // 32 bytes = 256 bits - matches the natural output size of SHA-256 used internally
    private const int HashSize = 32;
    // 100,000 rounds: controls how "expensive" (deliberately slow) computing a single hash is - the main
    // defense against brute-force, the "_" is just a digit separator for readability (100_000 == 100000 to the compiler)
    private const int Iterations = 100_000;
    // static readonly (not const) because HashAlgorithmName is a struct, not a primitive type - const requires a primitive/string type
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    // Returns a single "iterations.salt.hash" string that we'll store in the database
    // Called once, during registration only (AuthService.RegisterAsync)
    public static string Hash(string password)
    {
        // GetBytes uses the operating system's cryptographically secure random number generator (CSPRNG) -
        // deliberately *not* C#'s regular Random, because Random is not cryptographically secure
        // (its seed can sometimes be guessed/reproduced)
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        // The core call: runs PBKDF2 (based on HMAC-SHA256 repeated Iterations times) on the password+salt,
        // and returns HashSize bytes - this is the final "fingerprint" we'll store
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);
        // The three parts are stored together in one string, separated by dots, after Base64 encoding
        // (since salt/hash are raw bytes and can't be stored directly as readable text) -
        // this way a single row in the DB (User.PasswordHash) contains everything needed to verify a password
        // later, including the iteration count - which allows raising it in the future (more security) without
        // breaking verification for existing users
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // Checks a password against what's stored
    // Called on every login attempt (AuthService.LoginAsync)
    public static bool Verify(string password, string stored)
    {
        // Split('.', 3) - splits into at most 3 parts (the second parameter) - this way, even if there somehow
        // were an extra "." inside the Base64-encoded text, the split wouldn't produce too many parts
        string[] parts = stored.Split('.', 3);
        // Basic sanity check: if the stored string is malformed/not in the expected format, return false
        // (verification failed) instead of throwing an exception
        if (parts.Length != 3) return false;

        // Reads back the three parts we stored - they are *not* secret (the salt and iteration count are
        // fully known, even to anyone with access to the database) - the only secrecy here is the password
        // itself that the user is typing right now
        int iterations = int.Parse(parts[0]);
        byte[] salt = Convert.FromBase64String(parts[1]);
        byte[] expected = Convert.FromBase64String(parts[2]);

        // Runs the exact same PBKDF2 computation (with the same stored salt and iteration count) on the password just provided
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, HashSize);
        // FixedTimeEquals instead of "==" or a regular SequenceEqual: a normal comparison stops as soon as it
        // finds a differing byte, so the execution time "leaks" information (how many leading bytes were
        // correct) - a Timing Attack.
        // FixedTimeEquals always checks the full length, in constant time, regardless of how many bytes match -
        // closing off that vulnerability
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
