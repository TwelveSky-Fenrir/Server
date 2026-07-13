using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar297MonsterCount,
    ExpectedSize = 17)]
public readonly partial record struct ZoneWar297MonsterCountResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] MonsterNum { get; init; }
}
