using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar297MonsterCount,
    ExpectedSize = 17)]
public readonly partial record struct ZoneWar297MonsterCountResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] MonsterNum { get; init; }
}
