using System.Security.Cryptography;

namespace Fenrir.Data.Runtime;

internal static class SessionTicketCapability
{
    private const int NonceLength = 32;

    public static byte[] CreateHash()
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);

        try
        {
            return SHA256.HashData(nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
        }
    }
}
