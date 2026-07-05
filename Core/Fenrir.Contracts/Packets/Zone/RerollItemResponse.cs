using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Value: 6-int description of the resulting item (index, quantity, value, position...).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.RerollItem,
    ExpectedSize = 33)]
public readonly partial record struct RerollItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
