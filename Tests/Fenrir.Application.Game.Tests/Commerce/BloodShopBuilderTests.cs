using System.Collections.Frozen;
using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>Pure port of <c>MyDB::GetBloodShop</c> (S08_MyDB.cpp): BloodNum is unconditionally 50, and rows land at sequential positions starting at 0 regardless of their own BloodExchangeSlot number.</summary>
public class BloodShopBuilderTests
{
    private static ItemDefinition Item(int itemId, byte sort)
    {
        // Sort is ItemRowDto's param index 6 (0-based)
        var row = new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
        return new ItemDefinition(row, []);
    }

    [Fact]
    public void Build_BloodNumIsAlways50_RegardlessOfRealRowCount()
    {
        var shop = BloodShopBuilder.Build([new BloodExchangeCatalogRowDto(1, 12, 5, 0)],
            FrozenDictionary<int, ItemDefinition>.Empty);

        Assert.Equal(50, shop.BloodNum);
    }

    [Fact]
    public void Build_RealRowsLandAtSequentialIndexesStartingAtZero_NotAtTheirOwnSlotNumber()
    {
        var items = new Dictionary<int, ItemDefinition> { [12] = Item(12, 3), [1019] = Item(1019, 3) }
            .ToFrozenDictionary();

        var shop = BloodShopBuilder.Build(
            [new BloodExchangeCatalogRowDto(1, 12, 5, 0), new BloodExchangeCatalogRowDto(2, 1019, 9999, 1)], items);

        Assert.Equal(12, shop.Data[0].ItemId);
        Assert.Equal(5, shop.Data[0].Price);
        Assert.Equal(1019, shop.Data[1].ItemId);
        Assert.Equal(9999, shop.Data[1].Price);
        Assert.Equal(0, shop.Data[2].ItemId); // every other slot stays zeroed
    }

    [Fact]
    public void Build_ZeroQuantitySort99Item_IsForcedToOne()
    {
        var items = new Dictionary<int, ItemDefinition> { [12] = Item(12, 99) }.ToFrozenDictionary();

        var shop = BloodShopBuilder.Build([new BloodExchangeCatalogRowDto(1, 12, 5, 0)], items);

        Assert.Equal(1, shop.Data[0].Quantity);
    }

    [Fact]
    public void Build_ExcludesTheSentinelSlot100000()
    {
        var shop = BloodShopBuilder.Build([new BloodExchangeCatalogRowDto(100000, 6, 0, 0)],
            FrozenDictionary<int, ItemDefinition>.Empty);

        Assert.Equal(0, shop.Data[0].ItemId);
    }
}
