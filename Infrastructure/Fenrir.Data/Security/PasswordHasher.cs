using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Fenrir.Data.Security;

/// <summary>
///     Argon2id hashing; wire stays clear-text for legacy client compat, but nothing server-side stores/compares a
///     plain password.
/// </summary>
public static class PasswordHasher
{
    private const int MemorySizeKb = 64 * 1024;
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static (byte[] Hash, byte[] Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (HashWithSalt(password, salt), salt);
    }

    /// <summary>
    ///     Constant-time: <see cref="CryptographicOperations.FixedTimeEquals" /> never short-circuits on a differing
    ///     byte.
    /// </summary>
    public static bool Verify(string password, byte[] hash, byte[] salt)
    {
        return CryptographicOperations.FixedTimeEquals(HashWithSalt(password, salt), hash);
    }

    private static byte[] HashWithSalt(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySizeKb
        };

        return argon2.GetBytes(HashSize);
    }
}
