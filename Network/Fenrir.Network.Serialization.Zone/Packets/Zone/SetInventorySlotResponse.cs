using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SetInventorySlot,
    ExpectedSize = 33)]
public readonly partial record struct SetInventorySlotResponse : IOutgoingPacket
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
