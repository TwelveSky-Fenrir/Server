using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AddInventoryItem,
    ExpectedSize = 49)]
public readonly partial record struct AddInventoryItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int ItemIndex { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Xy { get; init; }

    public required int Quantity { get; init; }

    public required int Value { get; init; }

    public required int Serial { get; init; }

    [FixedArray(3)] public required int[] Socket { get; init; }

    public required int Expire { get; init; }
}
