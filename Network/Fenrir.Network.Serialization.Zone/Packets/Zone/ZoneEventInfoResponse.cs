using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneEventInfo,
    ExpectedSize = 135)]
public readonly partial record struct ZoneEventInfoResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
}
