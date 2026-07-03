using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_297_TYPE_REMAIN_MONSTER_INFO (ZONE.h:1084-1087) — "297" event, remaining monsters per tribe
///     (S07_MyGame01.cpp:11349), <c>BroadcastServer(1)</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar297MonsterCount,
    ExpectedSize = 17)]
public readonly partial record struct ZoneWar297MonsterCountResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] MonsterNum { get; init; }
}
