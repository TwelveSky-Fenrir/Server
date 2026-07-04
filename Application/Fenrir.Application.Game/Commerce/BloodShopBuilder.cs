using Fenrir.Application.Game.GameData;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Commerce;

/// <summary>
///     Pure port of <c>MyDB::GetBloodShop</c> (Server/ts25extra/S08_MyDB.cpp:269-299, verified byte-for-bit)
///     -- builds the 50-slot <see cref="BloodShop" /> wire snapshot from world.BloodExchangeCatalog. Two
///     corrections to the contract doc's own (wrong) paraphrase, verified against the real source: (1)
///     <c>BloodNum</c> is UNCONDITIONALLY 50, not "count of valid entries"; (2) the legacy's SQL has no
///     <c>ItemID != 0</c> filter, so its row count also includes now-dropped all-zero filler rows -- but
///     since those contribute only zeros anyway, iterating just the real rows in ascending
///     <see cref="BloodExchangeCatalogRowDto.BloodExchangeSlot" /> order and assigning sequential indices
///     from 0 reproduces the identical final byte layout.
/// </summary>
public static class BloodShopBuilder
{
    public const int MaxBloodSlots = 50;

    /// <summary><c>ItemSort99</c> (verified referenced, USE_MATS_999): a zero Quantity is forced to 1 for a Sort==99 item -- the exact same "coupon" convention <see cref="World.Npcs.NpcShopPolicy" /> and <see cref="World.Loot.GroundItemPickupPolicy" /> already special-case.</summary>
    private const byte ItemSort99 = 99;

    /// <summary><paramref name="rows" /> need not be pre-sorted or pre-filtered -- the sentinel (BloodExchangeSlot 100000) is excluded here.</summary>
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
