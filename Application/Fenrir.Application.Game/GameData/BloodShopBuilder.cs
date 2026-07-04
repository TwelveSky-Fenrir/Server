using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     Pure port of <c>MyDB::GetBloodShop</c> (Server/ts25extra/S08_MyDB.cpp:269-299, verified
///     byte-for-bit) -- builds the 50-slot <see cref="BloodShop" /> wire snapshot from
///     world.BloodExchangeCatalog. Two details the contract doc's own prose got WRONG, corrected here
///     after re-reading the real source directly (do not trust the paraphrase): (1) <c>BloodNum</c> is
///     UNCONDITIONALLY 50, not "count of valid entries" -- the legacy hardcodes <c>bsItem.aBloodNum = 50;</c>
///     regardless of how many rows were actually fetched; (2) the legacy's own SQL has NO <c>ItemID != 0</c>
///     filter (unlike <c>GetItemMall</c>'s), so <c>iRows</c> there also counts the dump's now-dropped
///     all-zero filler rows (Numbers 3-50) -- but since those rows contribute nothing but zeros at their
///     own array position anyway, iterating ONLY the real (kept) rows in ascending
///     <see cref="BloodExchangeCatalogRowDto.BloodExchangeSlot" /> order and assigning them the next
///     sequential array index from 0 reproduces the IDENTICAL final byte layout (real rows 1 and 2 land at
///     indices 0 and 1 either way; every other index is zero either way).
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
