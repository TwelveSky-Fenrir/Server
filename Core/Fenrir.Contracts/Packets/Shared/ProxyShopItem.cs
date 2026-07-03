using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     PROXY_SHOP_ITEM (STRUCT.h:1742-1749, 20 bytes, five ints, no padding) — one sale slot of an
///     offline/deputy personal shop, nested 25× inside <see cref="ProxyShopUserInfo" />.
/// </summary>
[FenrirWireType(20)]
public readonly partial record struct ProxyShopItem : IFenrirWireType<ProxyShopItem>
{
    public required int Id { get; init; }

    public required int Quantity { get; init; }

    public required int Value { get; init; }

    public required int Serial { get; init; }

    public required int Price { get; init; }
}
