using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SetInventorySlot,
    ExpectedSize = 33)]
public readonly record struct SetInventorySlotResponse : IOutgoingPacket
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
