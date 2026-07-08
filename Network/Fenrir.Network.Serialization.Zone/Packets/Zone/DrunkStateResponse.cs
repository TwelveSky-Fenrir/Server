using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DrunkState, ExpectedSize = 13)]
public readonly partial record struct DrunkStateResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Result { get; init; }
    public required int BottleIndex { get; init; }
}
