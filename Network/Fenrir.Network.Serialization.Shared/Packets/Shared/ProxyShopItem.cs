using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(20)]
public readonly partial record struct ProxyShopItem : IFenrirWireType<ProxyShopItem>
{
    public required int Id { get; init; }

    public required int Quantity { get; init; }

    public required int Value { get; init; }

    public required int Serial { get; init; }

    public required int Price { get; init; }
}
