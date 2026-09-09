using System.Security.Cryptography;

namespace NetSim.Server.Services;

public static class PasswordHasher
{
    private const int SaltSize = 16;        // גודל ה-salt (בייטים)
    private const int HashSize = 32;        // גודל ה-hash (32 = 256 ביט)
    private const int Iterations = 100_000; // כמה פעמים לחזור
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    // מחזיר מחרוזת אחת "iterations.salt.hash" שנשמור במסד
    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // בודק סיסמה מול מה ששמור
    public static bool Verify(string password, string stored)
    {
        string[] parts = stored.Split('.', 3);
        if (parts.Length != 3) return false;

        int iterations = int.Parse(parts[0]);
        byte[] salt = Convert.FromBase64String(parts[1]);
        byte[] expected = Convert.FromBase64String(parts[2]);

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, HashSize);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
