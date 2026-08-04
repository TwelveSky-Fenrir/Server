using System.Security.Cryptography;

namespace Fenrir.Data.Runtime;

internal static class SessionTicketCapability
{
    private const int CapabilityLength = 32;

    private const int EncodedCapabilityLength = 43;

    public static MintedCapability Mint()
    {
        var capability = RandomNumberGenerator.GetBytes(CapabilityLength);

        try
        {
            return new MintedCapability(Encode(capability), SHA256.HashData(capability));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(capability);
        }
    }

    public static bool TryHash(string capability, out byte[] hash)
    {
        hash = [];

        if (capability is null || capability.Length != EncodedCapabilityLength)
            return false;

        Span<char> base64 = stackalloc char[44];
        for (var index = 0; index < capability.Length; index++)
        {
            var character = capability[index];
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
                base64[index] = character;
            else if (character == '-')
                base64[index] = '+';
            else if (character == '_')
                base64[index] = '/';
            else
                return false;
        }

        base64[^1] = '=';

        Span<byte> decoded = stackalloc byte[CapabilityLength];
        if (!Convert.TryFromBase64Chars(base64, decoded, out var written) || written != CapabilityLength)
            return false;

        try
        {
            if (!string.Equals(Encode(decoded), capability, StringComparison.Ordinal))
                return false;

            hash = SHA256.HashData(decoded);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    internal readonly record struct MintedCapability(string Capability, byte[] Hash);
}
