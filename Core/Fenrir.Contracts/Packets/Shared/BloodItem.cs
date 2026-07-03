using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     BLOOD_ITEM (STRUCT.h:1423-1428, 12 bytes) — one slot of the blood-mark shop, nested 50× inside
///     <see cref="BloodShop" />. Declared outside pack(1) but 3 × int → no padding.
///     ATTENTION to field order: <see cref="Price" /> comes BEFORE <see cref="Quantity" /> (the
///     reverse of <see cref="ProxyShopItem" />, which is Id/Quantity/Value/Serial/Price).
/// </summary>
[FenrirWireType(12)]
public readonly partial record struct BloodItem : IFenrirWireType<BloodItem>
{
    public required int ItemId { get; init; }

    public required int Price { get; init; }

    public required int Quantity { get; init; }
}
