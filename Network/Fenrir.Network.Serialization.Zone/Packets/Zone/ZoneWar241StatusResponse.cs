using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar241Status,
    ExpectedSize = 5)]
public readonly record struct ZoneWar241StatusResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
