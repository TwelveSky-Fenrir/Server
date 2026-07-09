using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneWar049Status,
    ExpectedSize = 21)]
public readonly record struct ZoneWar049StatusResponse : IOutgoingPacket
{
    [FixedArray(4)] public required int[] TribeUserNum { get; init; }
    public required int RemainTime { get; init; }
}
