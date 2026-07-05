using System.Text;
using Fenrir.Network.Compression;

namespace Fenrir.Application.Game.Domain;

/// <summary>Decodes the USE_XOR_UID-obfuscated tID the legacy client relays ("MG"+decimal(AccountId), XORed).</summary>
public static class ObfuscatedUidCodec
{
    public static bool TryDecodeAccountId(string obfuscatedId, out int accountId)
    {
        var bytes = Encoding.Latin1.GetBytes(obfuscatedId);
        WireXor.ApplyUidXor(bytes);
        var deobfuscated = Encoding.Latin1.GetString(bytes);

        if (deobfuscated.StartsWith("MG", StringComparison.Ordinal))
            return int.TryParse(deobfuscated.AsSpan(2), out accountId);
        accountId = 0;
        return false;
    }
}
