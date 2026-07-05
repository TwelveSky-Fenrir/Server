using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Money is the dissolution refund; Value is the emptied slot / compensation stone.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DestroyItem, ExpectedSize = 33)]
public readonly partial record struct DestroyItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Money { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
