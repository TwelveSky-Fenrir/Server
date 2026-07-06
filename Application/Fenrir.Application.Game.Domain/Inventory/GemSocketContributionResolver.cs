using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>The eight requested-stat-type codes any caller actually passes -- <c>GetSocketType</c>'s <c>tType</c>.</summary>
public enum GemSocketStatRequest
{
    AttackPower = 1,
    Defense = 2,
    Life = 3,
    Mana = 4,
    AttackSuccess = 5,
    AttackBlock = 6,
    ElementalAttack = 7,
    ElementalDefense = 8
}

/// <summary>
///     Sums a socketed gem's contribution to one requested derived stat, across every active socket of one
///     equipped item -- <c>MyUtil::GetSocketInfo</c> / <c>GetSocketType</c> (<c>SOCKET_GEM_V2</c> encoding).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:10107-10148 (<c>GetSocketInfo</c>, unpacks 3 packed ints
///     into <c>SOCKET_GEM_V2</c> and sums <c>GetSocketType</c> per active socket) ; :10150-10464
///     (<c>GetSocketType</c>, the full stat-type/socket-type/gem-tier routing switch ; :10440-10461 is the
///     unguarded <c>mGSOCKET.Search(...)</c> dereference) ; Server/Header/Protocol/STRUCT.h:1882-1895
///     (<c>SOCKET_GEM_V2</c>'s 12-byte layout) ; Server/Header/Protocol/MyFactor.cpp:4031-4035 (AttackPower's
///     <c>GetSocketInfo(1,...)</c> call site, unconditional -- the only one of the eight not gated by
///     <c>USE_SOCKET_GEM</c>) ; :2118-2120/2304-2310/4110-4116/4188-4195/4236-4244/4305-4311/4356-4362
///     (Life/Mana/Defense/AttackSuccess/AttackBlock/ElementalAttack/ElementalDefense's own calls, each gated
///     by <c>USE_SOCKET_GEM</c>) ; Server/Header/Protocol/DEFINE.h:54 (unconditional
///     <c>#define USE_SOCKET_GEM</c>) ; :103-108 (<c>#ifdef LNW33</c> block <c>#undef</c>-ing it) ;
///     ServerDocs/03_BUILD_VARIANTS_ET_PREPROCESSEUR.md (confirms LNW33 is active in the only buildable
///     production configuration, corroborating the dead-code finding) ;
///     ServerDocs/12_ts25zone/12_MyGame03_PartieB.md §2.1 (<c>SOCKET_GEM_V2</c> layout and the
///     <c>GetSocketInfo</c>/<c>GetSocketType</c> pairing, cross-checked against a direct read).
///     <para>
///         Only <see cref="GemSocketStatRequest.AttackPower" /> is <see cref="IsLiveInProduction" /> -- the other
///         seven request codes are present in the legacy source but never compiled into the shipped server.
///         Wiring this resolver's contribution into the C# stat calculator for anything other than AttackPower
///         would be a deviation from legacy parity (a new feature the legacy never shipped), not a gap closure.
///     </para>
///     <para>
///         <see cref="GetSocketInfo" />'s <c>socketValueLookup</c> delegate is injected, not a hardcoded table:
///         the actual (requested-stat-type, gem-type, gem-tier) -&gt; value routing performed by the legacy's
///         ~315-line <c>GetSocketType</c> switch was not transcribed into the originating contract -- only its
///         shape was (route to one of two columns, <c>mValue03</c>/<c>mValue04</c>, of a
///         <c>world.GemSockets</c>-shaped row keyed by gem type + tier). Do not hardcode a guessed mapping here;
///         get a follow-up contract citing <c>GetSocketType</c>'s full body first.
///     </para>
///     A (gem-type, gem-tier) combination absent from the real gem-socket table is an unguarded
///     null-pointer dereference (a crash) in the legacy server (S07_MyGame03.cpp:10440-10461,
///     independently corroborated by ServerDocs/12_ts25zone/20_GameSystem_Monster_Npc_Quest_Pet_Socket.md
///     §9). This resolver deliberately does not reproduce that crash: an absent combination is expected to
///     be handled by the caller's own <c>socketValueLookup</c> (e.g. returning zero) -- a deliberate
///     deviation from legacy parity, not an oversight.
/// </remarks>
public static class GemSocketContributionResolver
{
    /// <summary>SOCKET_GEM_V2's own cap -- t1..t5/g1..g5.</summary>
    public const int MaxSocketsPerItem = 5;

    /// <summary>True only for <see cref="GemSocketStatRequest.AttackPower" /> -- see remarks.</summary>
    public static bool IsLiveInProduction(GemSocketStatRequest statType)
    {
        return statType == GemSocketStatRequest.AttackPower;
    }

    /// <summary>
    ///     Decodes <c>SOCKET_GEM_V2</c> (12 bytes packed little-endian into 3 ints: an unused byte, an
    ///     active-socket-count byte, then 5x(type,tier) byte pairs) and returns only the leading
    ///     active-count entries -- trailing unused slots are never consulted, matching the legacy's own
    ///     <c>for (i=0;i&lt;sg.num;i++)</c> bound.
    /// </summary>
    public static ImmutableArray<SocketEntry> UnpackActiveSockets(int packedSockets1, int packedSockets2,
        int packedSockets3)
    {
        Span<byte> bytes = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes[..4], packedSockets1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(4, 4), packedSockets2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8, 4), packedSockets3);

        var activeCount = Math.Min((int)bytes[1], MaxSocketsPerItem);
        if (activeCount == 0)
            return ImmutableArray<SocketEntry>.Empty;

        var builder = ImmutableArray.CreateBuilder<SocketEntry>(activeCount);
        for (var i = 0; i < activeCount; i++)
            builder.Add(new SocketEntry(bytes[2 + i * 2], bytes[3 + i * 2]));

        return builder.MoveToImmutable();
    }

    /// <summary>
    ///     Sums <paramref name="socketValueLookup" /> across every active socket, skipping any socket whose
    ///     gem type is zero without invoking the delegate at all -- mirrors the legacy's own
    ///     <c>if (a2[i][0]) val += GetSocketType(...)</c> guard.
    /// </summary>
    public static int GetSocketInfo(
        GemSocketStatRequest statType,
        int packedSockets1, int packedSockets2, int packedSockets3,
        Func<GemSocketStatRequest, SocketEntry, int> socketValueLookup)
    {
        var total = 0;
        foreach (var entry in UnpackActiveSockets(packedSockets1, packedSockets2, packedSockets3))
            if (entry.GemType != 0)
                total += socketValueLookup(statType, entry);

        return total;
    }

    /// <summary>One active socket's (gem type, gem tier) pair -- SOCKET_GEM_V2's tN/gN fields.</summary>
    public readonly record struct SocketEntry(byte GemType, byte GemTier);
}
