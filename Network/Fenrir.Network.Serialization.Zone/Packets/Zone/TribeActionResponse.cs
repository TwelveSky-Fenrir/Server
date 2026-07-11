using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAction, ExpectedSize = 109)]
public readonly partial record struct TribeActionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(100)] public required byte[] Data { get; init; }
}
