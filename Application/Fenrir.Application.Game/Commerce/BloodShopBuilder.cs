using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Commerce;

/// <summary>
///     Builds the 50-slot <see cref="BloodShop" /> wire snapshot. <c>BloodNum</c> is unconditionally 50, not a count
///     of valid entries (verified against legacy).
/// </summary>
public static class BloodShopBuilder
{
    public const int MaxBloodSlots = 50;

    /// <summary>
    ///     Same "coupon" convention as NpcShopPolicy/GroundItemPickupPolicy: a zero Quantity is forced to 1 for a
    ///     Sort==99 item.
    /// </summary>
    private const byte ItemSort99 = 99;

    /// <summary>
    ///     <paramref name="rows" /> need not be pre-sorted or pre-filtered -- the sentinel (slot 100000) is excluded
    ///     here.
    /// </summary>
    public static BloodShop Build(IEnumerable<BloodExchangeCatalogRowDto> rows,
        IReadOnlyDictionary<int, ItemDefinition> itemsById)
    {
        var data = new BloodItem[MaxBloodSlots];

        var i = 0;
        foreach (var row in rows.Where(static r => r.BloodExchangeSlot is >= 1 and <= 50 && r.ItemId is not null)
                     .OrderBy(static r => r.BloodExchangeSlot))
        {
            if (i >= MaxBloodSlots)
                break; // overflow guard -- never hit by real seed data (2 rows)

            var quantity = row.Quantity;
            if (quantity == 0 && itemsById.TryGetValue(row.ItemId!.Value, out var itemDefinition) &&
                itemDefinition.Item.Sort == ItemSort99)
                quantity = 1;

            data[i] = new BloodItem { ItemId = row.ItemId!.Value, Price = row.Cost, Quantity = quantity };
            i++;
        }

        for (; i < MaxBloodSlots; i++)
            data[i] = new BloodItem { ItemId = 0, Price = 0, Quantity = 0 };

        return new BloodShop { BloodNum = MaxBloodSlots, Data = data };
    }
}
