using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar241Status,
    ExpectedSize = 5)]
public readonly record struct ZoneWar241StatusResponse : IOutgoingPacket
{
    public required int RemainTime { get; init; }
}
