using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(12)]
public readonly partial record struct BloodItem : IFenrirWireType<BloodItem>
{
    public required int ItemId { get; init; }

    public required int Price { get; init; }

    public required int Quantity { get; init; }
}
