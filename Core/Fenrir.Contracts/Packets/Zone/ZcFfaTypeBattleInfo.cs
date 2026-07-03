using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_FFA_TYPE_BATTLE_INFO (ZONE.h:953-956, macro <c>ZCP_FFA_TYPE_BATTLE_INFO 200</c> declared at
///     l.1591, intercalated between the 100 and 101 defines) — FFA map 335 event
///     (S07_MyGame01.cpp:10909, <c>mZone335Type*</c> variables), remaining time every 10s,
///     <c>BroadcastServer(1)</c>. The twin countdown ZC 198 (335_TYPE_BATTLE_COUNTDOWN) is dead — FFA
///     only uses 200.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FfaTypeBattleInfo,
    ExpectedSize = 5)]
public readonly partial record struct ZcFfaTypeBattleInfo : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
