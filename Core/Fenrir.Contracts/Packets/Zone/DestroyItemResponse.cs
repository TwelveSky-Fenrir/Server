using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Money is the dissolution refund; Value is the emptied slot / compensation stone.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DestroyItem, ExpectedSize = 33)]
public readonly partial record struct DestroyItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Money { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
