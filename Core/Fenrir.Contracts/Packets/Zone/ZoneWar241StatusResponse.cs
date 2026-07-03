using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_241_TYPE_BATTLE_INFO (ZONE.h:980-983) — "241 LOD" map event tick: broadcast
///     (S07_MyGame01.cpp:11677) and per-instance unicast (l.11925, every 20 ticks); LOD start
///     (S07_MyGame03.cpp:6336, unicast); battle-end sites (S07_MyGame01.cpp:2767/2816/2837). Mixed
///     broadcast/unicast usage.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar241Status,
    ExpectedSize = 5)]
public readonly partial record struct ZoneWar241StatusResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
