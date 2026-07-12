using System.Text;
using Fenrir.Network.Compression;

namespace Fenrir.Application.Game.Domain;

// Wire-format compatibility only (legacy "MG<uUserIdx>" + XOR, Server/ts25login/S04_MyWork02.cpp:321,
// Server/Header/function.h:2874-2900) -- the decoded id is never itself proof of identity. Authorization
// is decided entirely by ISessionTicketRepository's DB-backed, single-use, expiring, shard-bound ticket;
// a decoded id with no matching ticket is always rejected regardless of what this method returns.
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
