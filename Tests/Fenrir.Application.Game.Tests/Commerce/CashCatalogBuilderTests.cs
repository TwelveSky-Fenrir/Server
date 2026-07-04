using Fenrir.Application.Game.Commerce;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>
///     Pure port of <c>MyDB::GetItemMall</c> (S08_MyDB.cpp:137-235) -- <see cref="CashCatalogBuilder" />'s
///     own remarks document the exact source lines each assertion below pins.
/// </summary>
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
            Product(1, 1, itemId: 100, quantity: 0, cost: 20, active: true),
            Product(2, 1, itemId: 101, quantity: 0, cost: 30, active: true),
            Product(3, 2, itemId: 200, quantity: 5, cost: 40, active: true)
        ]);

        Assert.Equal(100, catalog.CostInfoByIndex[0].ItemId);
        Assert.Equal(101, catalog.CostInfoByIndex[1].ItemId);
        // Type 2's block starts at index 200 (MaxCashPage * MaxCashItemPerPage), not index 2.
        Assert.Equal(200, catalog.CostInfoByIndex[200].ItemId);
        Assert.False(catalog.CostInfoByIndex[2].IsAssigned);
    }

    [Fact]
    public void Build_InactiveProduct_StillConsumesACostInfoIndexAndADisplayCursorPosition_ButLeavesTheGridSlotAtMinusOne()
    {
        // Verified quirk (S08_MyDB.cpp:213-226): itemCount/pageCount advance for EVERY qualifying row
        // regardless of Active, but tDbCash (the display grid) is only written `if (tDBActive[...])`.
        var catalog = CashCatalogBuilder.Build([
            Product(1, 1, itemId: 100, quantity: 0, cost: 20, active: false), // display slot 0 -- inactive, stays -1
            Product(2, 1, itemId: 101, quantity: 0, cost: 30, active: true) // display slot 1 -- active
        ]);

        // Slot 0 (type 0, page 0, item 0): still -1 (never written).
        Assert.Equal(-1, catalog.DisplayGrid[0]);
        // Slot 1 (type 0, page 0, item 1): costInfoIndex=1, itemId=101, quantity=0, cost=30.
        var baseIndex = (0 * CashCatalogBuilder.MaxCashItemPerPage + 1) * CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(1, catalog.DisplayGrid[baseIndex + 0]);
        Assert.Equal(101, catalog.DisplayGrid[baseIndex + 1]);
        Assert.Equal(0, catalog.DisplayGrid[baseIndex + 2]);
        Assert.Equal(30, catalog.DisplayGrid[baseIndex + 3]);

        // But the INACTIVE row's own costInfoIndex (0) is still populated in the master table --
        // a purchase attempt against it would still resolve a real (Cost, ItemId, Quantity, Type), the
        // client's own catalog UI simply never shows it as a buyable tile.
        Assert.True(catalog.CostInfoByIndex[0].IsAssigned);
        Assert.Equal(100, catalog.CostInfoByIndex[0].ItemId);
    }

    [Fact]
    public void Build_TenthItemOnAPage_RollsOverToTheNextPage()
    {
        var products = Enumerable.Range(1, 11)
            .Select(i => Product(i, 1, itemId: 1000 + i, quantity: 0, cost: 10, active: true))
            .ToArray();

        var catalog = CashCatalogBuilder.Build(products);

        // Item 11 (the 11th, index 10) lands on page 1, item-slot 0.
        var baseIndex = ((0 * CashCatalogBuilder.MaxCashPage + 1) * CashCatalogBuilder.MaxCashItemPerPage + 0) *
                         CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(1011, catalog.DisplayGrid[baseIndex + 1]);
    }

    [Fact]
    public void Build_OutOfRangeItemId_ConsumesACostInfoIndex_ButNotADisplayCursorPosition()
    {
        // Regression test (review finding): the legacy's SECOND pass (S08_MyDB.cpp:182-185) `continue`s on
        // an ItemID outside [1,99999] BEFORE the itemCount++/pageCount++ that follows -- the row still
        // consumed a costInfoIndex slot (the legacy's FIRST pass wrote it unconditionally), but must NOT
        // consume a display-grid cursor position, so the FOLLOWING valid row lands on the SAME display slot
        // the out-of-range row would have occupied, not the next one.
        var catalog = CashCatalogBuilder.Build([
            Product(1, 1, itemId: 100_000, quantity: 0, cost: 20, active: true), // out of [1,99999] -- costInfoIndex 0
            Product(2, 1, itemId: 101, quantity: 0, cost: 30, active: true) // in range -- costInfoIndex 1
        ]);

        // Both rows still get their own master cost-info slot.
        Assert.Equal(100_000, catalog.CostInfoByIndex[0].ItemId);
        Assert.Equal(101, catalog.CostInfoByIndex[1].ItemId);

        // But the display grid's FIRST slot (page 0, item 0) is the in-range row (costInfoIndex 1), not -1
        // and not the out-of-range row.
        var baseIndex = (0 * CashCatalogBuilder.MaxCashItemPerPage + 0) * CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(1, catalog.DisplayGrid[baseIndex + 0]);
        Assert.Equal(101, catalog.DisplayGrid[baseIndex + 1]);

        // No second display slot was consumed by the out-of-range row -- slot 1 stays untouched (-1).
        var secondSlotBaseIndex = (0 * CashCatalogBuilder.MaxCashItemPerPage + 1) * CashCatalogBuilder.MaxCashItemDetail;
        Assert.Equal(-1, catalog.DisplayGrid[secondSlotBaseIndex + 0]);
    }

    [Fact]
    public void Build_IgnoresProductType5AndNullItemId()
    {
        var catalog = CashCatalogBuilder.Build([
            Product(100000, 5, itemId: 6, quantity: 0, cost: 0, active: true), // version sentinel, excluded
            new ItemMallProductRowDto(2, 1, null, 0, 0, false) // ItemID=0 -> NULL, excluded (ItemID != 0 filter)
        ]);

        Assert.False(catalog.CostInfoByIndex[0].IsAssigned);
        Assert.All(catalog.DisplayGrid, v => Assert.Equal(-1, v));
    }

    [Fact]
    public void ResolveVersion_ReturnsTheSentinelRowsItemId()
    {
        var version = CashCatalogBuilder.ResolveVersion([
            Product(1, 1, itemId: 100, quantity: 0, cost: 20, active: true),
            Product(100000, 5, itemId: 6, quantity: 0, cost: 0, active: true)
        ]);

        Assert.Equal(6, version);
    }

    [Fact]
    public void ResolveVersion_NoSentinelRow_ReturnsZero()
    {
        var version = CashCatalogBuilder.ResolveVersion([Product(1, 1, itemId: 100, quantity: 0, cost: 20, active: true)]);

        Assert.Equal(0, version);
    }
}
