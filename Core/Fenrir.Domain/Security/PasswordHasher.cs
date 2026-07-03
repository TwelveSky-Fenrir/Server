using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Fenrir.Domain.Security;

/// <summary>
///     Argon2id account password hashing (architecture reference §9.1: m=64 MiB, t=3, p=1, recalibrated by an
///     annual benchmark). Pure — no SQL, no network — so it belongs in Core: the wire stays clear-text for legacy
///     client compatibility (ADR-0003 A-05), but nothing server-side ever stores or compares a plain password.
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
    ///     Constant-time by construction: <see cref="CryptographicOperations.FixedTimeEquals" /> never short-circuits on
    ///     the first differing byte.
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
