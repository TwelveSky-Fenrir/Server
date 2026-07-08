using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar297MonsterCount,
    ExpectedSize = 17)]
public readonly record struct ZoneWar297MonsterCountResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] MonsterNum { get; init; }
}
