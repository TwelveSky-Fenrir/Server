using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.GameData;

public static class BloodShopBuilder
{
    public const int MaxBloodSlots = 50;

    private const byte ItemSort99 = 99;

    public static BloodShop Build(IEnumerable<BloodExchangeCatalogRowDto> rows,
        IReadOnlyDictionary<int, ItemDefinition> itemsById)
    {
        var data = new BloodItem[MaxBloodSlots];

        var i = 0;
        foreach (var row in rows.Where(static r => r.BloodExchangeSlot is >= 1 and <= 50 && r.ItemId is not null)
                     .OrderBy(static r => r.BloodExchangeSlot))
        {
            if (i >= MaxBloodSlots)
                break;

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

    public static int ResolveVersion(IEnumerable<BloodExchangeCatalogRowDto> rows)
    {
        foreach (var row in rows)
            if (row.BloodExchangeSlot == 100000)
                return row.ItemId ?? 0;

        return 0;
    }
}
