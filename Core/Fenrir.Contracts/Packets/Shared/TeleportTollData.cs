using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     MONEY_NO_TELEPORT_RECV (STRUCT.h:1266-1269, 4 bytes) — the re-read layer <c>MyWork::ProcessForData</c>
///     applies over CZ_PROCESS_DATA_SEND's <c>tData</c> blob (payload offset 4) for tSort 207 ("paying NPC"
///     instant teleport toll, V5 NPC &amp; Economy). Carries ONLY the toll amount -- verified in the source,
///     NOT a truncated read: the legacy resolves the actual destination entirely client-side and separately,
///     via the ALREADY-EXISTING CZ_DEMAND_ZONE_SERVER_INFO_2 (opcode 20) Sort=5 ("paying NPC") path
///     <c>ZoneMoveHandler</c> already handles generically. Not a packet of its own, same treatment as
///     <see cref="DefaultPData" />.
/// </summary>
[FenrirWireType(4)]
public readonly partial record struct TeleportTollData : IFenrirWireType<TeleportTollData>
{
    /// <summary>Legacy <c>tMoney</c> -- bounds [0, 100000000] verified at S04_MyWork04.cpp:432.</summary>
    public required int Money { get; init; }
}
