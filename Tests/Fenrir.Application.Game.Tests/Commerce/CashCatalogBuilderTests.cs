using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Commerce;

public class CashCatalogBuilderTests
{
    private static ItemMallProductRowDto Product(int id, byte type, int itemId, int quantity, int cost, bool active)
    {
        return new ItemMallProductRowDto(id, type, itemId, quantity, cost, active);
    }

    [Fact]
    public void Build_AssignsSequentialCostInfoIndexes_PerTypeBlockOf200()
    {
        var catalog = CashCatalogBuilder.Build([
            Product(1, 1, 100, 0, 20, true),
            Product(2, 1, 101, 0, 30, true),
            Product(3, 2, 200, 5, 40, true)
        ]);

        Assert.Equal(100, catalog.CostInfoByIndex[0].ItemId);
        Assert.Equal(101, catalog.CostInfoByIndex[1].ItemId);
        Assert.Equal(200, catalog.CostInfoByIndex[200].ItemId);
        Assert.False(catalog.CostInfoByIndex[2].IsAssigned);
    }

    [Fact]
    public void
        Build_InactiveProduct_StillConsumesACostInfoIndexAndADisplayCursorPosition_ButLeavesTheGridSlotAtMinusOne()
    {
        var catalog = CashCatalogBuilder.Build([
            Product(1, 1, 100, 0, 20, false),
            Product(2, 1, 101, 0, 30, true)
        ]);

        Assert.Equal(-1, catalog.DisplayGrid[0]);
        var baseIndex = (0 * CashCatalogBuilder.MaxCashItemPerPage + 1) * CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(1, catalog.DisplayGrid[baseIndex + 0]);
        Assert.Equal(101, catalog.DisplayGrid[baseIndex + 1]);
        Assert.Equal(0, catalog.DisplayGrid[baseIndex + 2]);
        Assert.Equal(30, catalog.DisplayGrid[baseIndex + 3]);

        Assert.True(catalog.CostInfoByIndex[0].IsAssigned);
        Assert.Equal(100, catalog.CostInfoByIndex[0].ItemId);
    }

    [Fact]
    public void Build_TenthItemOnAPage_RollsOverToTheNextPage()
    {
        var products = Enumerable.Range(1, 11)
            .Select(i => Product(i, 1, 1000 + i, 0, 10, true))
            .ToArray();

        var catalog = CashCatalogBuilder.Build(products);

        var baseIndex = ((0 * CashCatalogBuilder.MaxCashPage + 1) * CashCatalogBuilder.MaxCashItemPerPage + 0) *
                        CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(1011, catalog.DisplayGrid[baseIndex + 1]);
    }

    [Fact]
    public void Build_OutOfRangeItemId_ConsumesACostInfoIndex_ButNotADisplayCursorPosition()
    {
        var catalog = CashCatalogBuilder.Build([
            Product(1, 1, 100_000, 0, 20, true),
            Product(2, 1, 101, 0, 30, true)
        ]);

        Assert.Equal(100_000, catalog.CostInfoByIndex[0].ItemId);
        Assert.Equal(101, catalog.CostInfoByIndex[1].ItemId);

        var baseIndex = (0 * CashCatalogBuilder.MaxCashItemPerPage + 0) * CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(1, catalog.DisplayGrid[baseIndex + 0]);
        Assert.Equal(101, catalog.DisplayGrid[baseIndex + 1]);

        var secondSlotBaseIndex =
            (0 * CashCatalogBuilder.MaxCashItemPerPage + 1) * CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(-1, catalog.DisplayGrid[secondSlotBaseIndex + 0]);
    }

    [Fact]
    public void Build_IgnoresProductType5AndNullItemId()
    {
        var catalog = CashCatalogBuilder.Build([
            Product(100000, 5, 6, 0, 0, true),
            new ItemMallProductRowDto(2, 1, null, 0, 0, false)
        ]);

        Assert.False(catalog.CostInfoByIndex[0].IsAssigned);
        Assert.All(catalog.DisplayGrid, v => Assert.Equal(-1, v));
    }

    [Fact]
    public void ResolveVersion_ReturnsTheSentinelRowsItemId()
    {
        var version = CashCatalogBuilder.ResolveVersion([
            Product(1, 1, 100, 0, 20, true),
            Product(100000, 5, 6, 0, 0, true)
        ]);

        Assert.Equal(6, version);
    }

    [Fact]
    public void ResolveVersion_NoSentinelRow_ReturnsZero()
    {
        var version = CashCatalogBuilder.ResolveVersion([Product(1, 1, 100, 0, 20, true)]);

        Assert.Equal(0, version);
    }

    [Fact]
    public void ResolveCrc_ReturnsTheDedicatedSentinelRowsItemId_DistinctFromTheVersionRow()
    {
        var crc = CashCatalogBuilder.ResolveCrc([
            Product(1, 1, 100, 0, 20, true),
            Product(100000, 5, 6, 0, 0, true),
            Product(100001, 5, 99, 0, 0, true)
        ]);

        Assert.Equal(99, crc);
    }

    [Fact]
    public void ResolveCrc_NoSentinelRow_ReturnsZero()
    {
        var crc = CashCatalogBuilder.ResolveCrc([
            Product(1, 1, 100, 0, 20, true),
            Product(100000, 5, 6, 0, 0, true)
        ]);

        Assert.Equal(0, crc);
    }
}
