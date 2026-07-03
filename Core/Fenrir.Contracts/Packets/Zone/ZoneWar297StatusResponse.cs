using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_297_TYPE_REMAIN_INFO (ZONE.h:1077-1082) — "297" monster-wave event (server variables
///     <c>mZone200Type*</c>): opening countdown (S07_MyGame01.cpp:11274), remaining time
///     (l.11353/11459), <c>BroadcastServer(1)</c>. Same shape as the dead ZC 115 (DICE_BATTLE) — not
///     shared, per spec.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar297Status,
    ExpectedSize = 13)]
public readonly partial record struct ZoneWar297StatusResponse : IOutgoingPacket
{
    public required int Value00 { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
