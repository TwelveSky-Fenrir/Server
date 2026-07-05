using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

// Field order: Price before Quantity (opposite of ProxyShopItem).
[FenrirWireType(12)]
public readonly partial record struct BloodItem : IFenrirWireType<BloodItem>
{
    public required int ItemId { get; init; }

    public required int Price { get; init; }

    public required int Quantity { get; init; }
}
