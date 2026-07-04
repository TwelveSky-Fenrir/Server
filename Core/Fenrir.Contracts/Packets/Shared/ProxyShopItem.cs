using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

// One sale slot of an offline/deputy personal shop; nested 25x inside ProxyShopUserInfo.
[FenrirWireType(20)]
public readonly partial record struct ProxyShopItem : IFenrirWireType<ProxyShopItem>
{
    public required int Id { get; init; }

    public required int Quantity { get; init; }

    public required int Value { get; init; }

    public required int Serial { get; init; }

    public required int Price { get; init; }
}
