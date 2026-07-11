using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(12)]
public readonly partial record struct BloodItem : IFenrirWireType<BloodItem>
{
    public required int ItemId { get; init; }

    public required int Price { get; init; }

    public required int Quantity { get; init; }
}
