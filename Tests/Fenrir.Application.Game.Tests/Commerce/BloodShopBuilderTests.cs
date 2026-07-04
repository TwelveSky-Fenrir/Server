using System.Collections.Frozen;
using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>
///     Pure port of <c>MyDB::GetBloodShop</c> (S08_MyDB.cpp:269-299) -- pins the two corrections to the
///     contract doc's own (verified wrong) paraphrase: BloodNum is unconditionally 50, and real rows land
///     at sequential array positions starting at 0 regardless of their own BloodExchangeSlot number.
/// </summary>
public class BloodShopBuilderTests
{
    private static ItemDefinition Item(int itemId, byte sort)
    {
        // Positional ItemRowDto(ItemId,Name,Desc1,Desc2,Desc3, Type,Sort,DataNumber2D,DataNumber3D,
        // AddDataNumber3D, Level,MartialLevel,EquipInfo1,EquipInfo2, BuyCost,SellCost,BuyCost2,LevelLimit,
        // MartialLevelLimit, ...) -- Sort is param index 6 (0-based), NOT wherever a copy-pasted helper's
        // own "vitality" slot happened to sit (a review-caught bug in an earlier draft of this exact file).
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
        var items = new Dictionary<int, ItemDefinition> { [12] = Item(12, sort: 3), [1019] = Item(1019, sort: 3) }
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
        var items = new Dictionary<int, ItemDefinition> { [12] = Item(12, sort: 99) }.ToFrozenDictionary();

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
