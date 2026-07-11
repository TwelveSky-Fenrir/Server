using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.World.Npcs;

public class WarPointShopPolicyTests
{
    private const byte StackableSort = 2;
    private const byte NonStackableSort = 9;
    private const int WpNpc = WarPointShopCatalog.NobleDragonNpcId;
    private const int OtherWpNpc = WarPointShopCatalog.RoyalSerpentNpcId;
    private const int NangimNpc = WarPointShopCatalog.NangimNpcId;
    private const int OrdinaryNpc = 1;

    private static ItemDefinition Item(int itemId, byte sort)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = sort }, []);
    }

    private static WarPointPriceEntry Entry(int itemId, int wp, int cp, params int[] displayNpcs)
    {
        return new WarPointPriceEntry(itemId, wp, cp, [.. displayNpcs]);
    }

    private static WarPointShopCatalog Catalog(params WarPointPriceEntry[] entries)
    {
        return new WarPointShopCatalog(entries);
    }

    private static ItemStack Stack(int itemId, int quantity)
    {
        return new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void OrdinaryNpc_IsNeverAWarPointTransaction()
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc, OrdinaryNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, OrdinaryNpc, Item(90200, NonStackableSort), 1, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.NotWarPointItem, result.Outcome);
    }

    [Fact]
    public void WarPointNpc_ItemNotInPriceTable_FallsThroughToOrdinaryPath()
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90999, NonStackableSort), 1, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.NotWarPointItem, result.Outcome);
    }

    [Fact]
    public void EmptyCatalog_EveryRequestFallsThrough()
    {
        var result = WarPointShopPolicy.ResolveBuy(Catalog(), WpNpc, Item(90200, NonStackableSort), 1, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.NotWarPointItem, result.Outcome);
    }

    [Fact]
    public void Page2TribeItem_AtWrongNpc_IsHardRejectedBeforeAnyWarPointLogic()
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, OtherWpNpc, Item(90200, NonStackableSort), 1, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.WrongNpc, result.Outcome);
    }

    [Fact]
    public void Page2TribeItem_AtNangim_IsHardRejected()
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, NangimNpc, Item(90200, NonStackableSort), 1, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.WrongNpc, result.Outcome);
    }

    [Fact]
    public void NonStackable_EmptyDestination_ProceedsWithWarPointCostAndFreshStack()
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort), 1, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        Assert.Equal(500, result.WarPointCost);
        Assert.Equal(0, result.ContributionPointCost);
        Assert.Equal(90200, result.NewDestinationStack!.Value.ItemId);
        Assert.Equal(1, result.NewDestinationStack!.Value.Quantity);
    }

    [Fact]
    public void NonStackable_OccupiedDestination_IsHardRejected()
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort), 1,
            Stack(700, 1), 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void Stackable_MultiQuantity_ScalesWarPointCostByQuantity()
    {
        var catalog = Catalog(Entry(50, 20, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(50, StackableSort), 5, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        Assert.Equal(100, result.WarPointCost);
        Assert.Equal(5, result.NewDestinationStack!.Value.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Stackable_QuantityBelowOne_IsCoercedUpToOne(int requestedQuantity)
    {
        var catalog = Catalog(Entry(50, 20, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(50, StackableSort), requestedQuantity, null,
            0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        Assert.Equal(20, result.WarPointCost);
        Assert.Equal(1, result.NewDestinationStack!.Value.Quantity);
    }

    [Fact]
    public void Stackable_MergeIntoSameItem_CombinesQuantityAndScalesCostByPurchasedUnits()
    {
        var catalog = Catalog(Entry(50, 20, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(50, StackableSort), 5, Stack(50, 10), 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        Assert.Equal(100, result.WarPointCost);
        Assert.Equal(15, result.NewDestinationStack!.Value.Quantity);
    }

    [Fact]
    public void Stackable_MergeOverflowingStackCap_IsHardRejected()
    {
        var catalog = Catalog(Entry(50, 20, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(50, StackableSort), 5, Stack(50, 998), 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void Stackable_MergeIntoDifferentItem_IsHardRejected()
    {
        var catalog = Catalog(Entry(50, 20, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(50, StackableSort), 5, Stack(99, 10), 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void ContributionPointPriced_InsufficientCp_IsSoftRejected()
    {
        var catalog = Catalog(Entry(90200, 500, 100, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort), 1, null, 50);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.InsufficientContributionPoints, result.Outcome);
    }

    [Fact]
    public void ContributionPointPriced_SufficientCp_ProceedsWithBothCosts()
    {
        var catalog = Catalog(Entry(90200, 500, 100, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort), 1, null, 100);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        Assert.Equal(500, result.WarPointCost);
        Assert.Equal(100, result.ContributionPointCost);
    }

    [Fact]
    public void ContributionPointPriced_Stackable_ScalesCpByQuantityAndGatesOnScaledTotal()
    {
        var catalog = Catalog(Entry(50, 20, 10, WpNpc));

        var rejected = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(50, StackableSort), 5, null, 40);
        Assert.Equal(WarPointShopPolicy.BuyOutcome.InsufficientContributionPoints, rejected.Outcome);

        var proceeded = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(50, StackableSort), 5, null, 50);
        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, proceeded.Outcome);
        Assert.Equal(50, proceeded.ContributionPointCost);
        Assert.Equal(100, proceeded.WarPointCost);
    }

    [Fact]
    public void NonStackable_MultiQuantity_ScalesChargedCostButStillDeliversExactlyOneUnit()
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort), 5, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        Assert.Equal(2500, result.WarPointCost);
        Assert.Equal(1, result.NewDestinationStack!.Value.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NonStackable_QuantityNotPositive_ChargesUnscaledSingleUnitCost(int requestedQuantity)
    {
        var catalog = Catalog(Entry(90200, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort),
            requestedQuantity, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        Assert.Equal(500, result.WarPointCost);
        Assert.Equal(1, result.NewDestinationStack!.Value.Quantity);
    }

    [Fact]
    public void NonStackable_MultiQuantity_ScalesContributionPointCostAndGatesOnScaledTotal()
    {
        var catalog = Catalog(Entry(90200, 500, 10, WpNpc));

        var rejected = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort), 5, null, 40);
        Assert.Equal(WarPointShopPolicy.BuyOutcome.InsufficientContributionPoints, rejected.Outcome);

        var proceeded = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(90200, NonStackableSort), 5, null, 50);
        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, proceeded.Outcome);
        Assert.Equal(50, proceeded.ContributionPointCost);
        Assert.Equal(2500, proceeded.WarPointCost);
    }

    [Fact]
    public void FixedStateStampBand_86700To86725_IsFlaggedButGrantedPlain()
    {
        Assert.True(WarPointShopPolicy.IsFixedStateStampItem(86700));
        Assert.True(WarPointShopPolicy.IsFixedStateStampItem(86725));
        Assert.False(WarPointShopPolicy.IsFixedStateStampItem(86699));
        Assert.False(WarPointShopPolicy.IsFixedStateStampItem(86726));

        var catalog = Catalog(Entry(86700, 500, 0, WpNpc));
        var result = WarPointShopPolicy.ResolveBuy(catalog, WpNpc, Item(86700, NonStackableSort), 1, null, 0);

        Assert.Equal(WarPointShopPolicy.BuyOutcome.Proceed, result.Outcome);
        var stack = result.NewDestinationStack!.Value;
        Assert.Equal(0, stack.Enchant);
        Assert.Equal(0, stack.Combine);
        Assert.Equal(0, stack.Refine);
        Assert.Equal(0, stack.Socket);
    }
}
