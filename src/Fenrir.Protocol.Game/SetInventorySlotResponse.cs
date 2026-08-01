using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SetInventorySlot,
    ExpectedSize = 33)]
public readonly partial record struct SetInventorySlotResponse : IOutgoingPacket
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
