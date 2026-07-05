using System.Text;
using Fenrir.Contracts.Wire;

namespace Fenrir.Application.Game.Handlers;

/// <summary>Decodes the USE_XOR_UID-obfuscated tID the legacy client relays ("MG"+decimal(AccountId), XORed).</summary>
internal static class ObfuscatedUidCodec
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
