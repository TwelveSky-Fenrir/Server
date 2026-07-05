using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Value: 6-int description of the resulting item (index, quantity, value, position...).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.RerollItem,
    ExpectedSize = 33)]
public readonly partial record struct RerollItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
