using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Npcs;

public class NpcShopPolicyTests
{
    private static ItemStack Stack(int itemId, int quantity, byte enchant = 0, byte combine = 0, byte refine = 0,
        byte socket = 0)
    {
        return new ItemStack(itemId, quantity, enchant, combine, refine, socket, 0, 0, 0, 0, 0);
    }

    private static ItemDefinition Sellable(int itemId, byte sort, int sellCost, byte type = 0, byte checkNpcSell = 0)
    {
        var row = WorldDataTestRows.Item(itemId) with
        {
            Sort = sort, SellCost = sellCost, Type = type, CheckNpcSell = checkNpcSell
        };
        return new ItemDefinition(row, []);
    }

    private static ItemDefinition Buyable(int itemId, byte sort, int buyCost, int buyCost2 = 0, byte checkNpcShop = 2)
    {
        var row = WorldDataTestRows.Item(itemId) with
        {
            Sort = sort, BuyCost = buyCost, BuyCost2 = buyCost2, CheckNpcShop = checkNpcShop
        };
        return new ItemDefinition(row, []);
    }

    private static NpcDefinition Shop(int npcId, byte npcType, params int[] itemIds)
    {
        var shopItems = itemIds.Select((id, i) => new NpcShopItemRowDto(npcId, 0, (byte)i, id)).ToImmutableArray();
        return new NpcDefinition(WorldDataTestRows.Npc(npcId) with { Type = npcType }, [], shopItems, [], [], []);
    }

    [Fact]
    public void Sell_NonStackable_CreditsFlatSellCost_ClearsSlot()
    {
        var item = Sellable(700, 9, 500);
        var result = NpcShopPolicy.ResolveSell(item, Stack(700, 1), 0);

        Assert.True(result.Succeeded);
        Assert.Equal(500, result.MoneyGained);
        Assert.Null(result.RemainingSourceStack);
    }

    [Fact]
    public void Sell_Stackable_CreditsSellCostTimesQuantity_PartialStackRemains()
    {
        var item = Sellable(50, 2, 10);
        var result = NpcShopPolicy.ResolveSell(item, Stack(50, 20), 5);

        Assert.True(result.Succeeded);
        Assert.Equal(50, result.MoneyGained);
        Assert.Equal(15, result.RemainingSourceStack!.Value.Quantity);
    }

    [Fact]
    public void Sell_Stackable_SellingWholeStack_ClearsSlot()
    {
        var item = Sellable(50, 2, 10);
        var result = NpcShopPolicy.ResolveSell(item, Stack(50, 20), 20);

        Assert.True(result.Succeeded);
        Assert.Null(result.RemainingSourceStack);
    }

    [Fact]
    public void Sell_CheckNpcSellFlagSet_IsRejected()
    {
        var item = Sellable(700, 9, 500, checkNpcSell: 1);
        var result = NpcShopPolicy.ResolveSell(item, Stack(700, 1), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Sell_RareItemWithEnchantApplied_IsRejected()
    {
        var item = Sellable(700, 9, 500, 3);
        var result = NpcShopPolicy.ResolveSell(item, Stack(700, 1, 5), 0);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Sell_RareItemWithNoUpgradeApplied_Succeeds()
    {
        var item = Sellable(700, 9, 500, 3);
        var result = NpcShopPolicy.ResolveSell(item, Stack(700, 1), 0);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Sell_StackableQuantityExceedsHeld_IsRejected()
    {
        var item = Sellable(50, 2, 10);
        var result = NpcShopPolicy.ResolveSell(item, Stack(50, 5), 10);

        Assert.Equal(NpcShopPolicy.SellOutcome.InvalidQuantity, result.Outcome);
    }

    [Fact]
    public void Buy_ItemNotInNpcCatalog_IsRejected()
    {
        var npc = Shop(1, 1, 100);
        var item = Buyable(200, 9, 1000);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 1, 1, 0);

        Assert.Equal(NpcShopPolicy.BuyOutcome.NotInCatalog, result.Outcome);
    }

    [Fact]
    public void Buy_NonStackable_EmptyDestination_Succeeds_AtFlatCost()
    {
        var npc = Shop(1, 1, 700);
        var item = Buyable(700, 9, 1000);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 1, 1, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(1000, result.MoneyCost);
        Assert.Equal(0, result.CpCost);
        Assert.Equal(700, result.NewDestinationStack!.Value.ItemId);
        Assert.Equal(1, result.NewDestinationStack.Value.Quantity);
    }

    [Fact]
    public void Buy_NonStackable_OccupiedDestination_IsConflict()
    {
        var npc = Shop(1, 1, 700);
        var item = Buyable(700, 9, 1000);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, Stack(1, 1), 1, 1, 0);

        Assert.Equal(NpcShopPolicy.BuyOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void Buy_Stackable_MergesIntoExistingSameItemStack_CostsPerUnit()
    {
        var npc = Shop(1, 1, 50);
        var item = Buyable(50, 2, 100);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 5, Stack(50, 10), 1, 1, 0);

        Assert.True(result.Succeeded);
        Assert.Equal(500, result.MoneyCost);
        Assert.Equal(15, result.NewDestinationStack!.Value.Quantity);
    }

    [Fact]
    public void Buy_Stackable_MergeExceedingCap_IsConflict()
    {
        var npc = Shop(1, 1, 50);
        var item = Buyable(50, 2, 100);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 10, Stack(50, 995), 1, 1, 0);

        Assert.Equal(NpcShopPolicy.BuyOutcome.DestinationConflict, result.Outcome);
    }

    [Fact]
    public void Buy_ItemNotFlaggedForNpcShop_IsCleanFailure_NotDisconnect()
    {
        var npc = Shop(1, 1, 700);
        var item = Buyable(700, 9, 1000, checkNpcShop: 0);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 1, 1, 0);

        Assert.Equal(NpcShopPolicy.BuyOutcome.NotSellableHere, result.Outcome);
        Assert.True(result.IsCleanFailure);
    }

    [Fact]
    public void Buy_ContributionPointCostItem_SufficientBalance_Succeeds_ChargesCp()
    {
        var npc = Shop(1, 1, 700);
        var item = Buyable(700, 9, 1000, 50);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 1, 1, 50);

        Assert.True(result.Succeeded);
        Assert.Equal(1000, result.MoneyCost);
        Assert.Equal(50, result.CpCost);
    }

    [Fact]
    public void Buy_ContributionPointCostItem_InsufficientBalance_IsNotCleanFailure()
    {
        var npc = Shop(1, 1, 700);
        var item = Buyable(700, 9, 1000, 50);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 1, 1, 49);

        Assert.Equal(NpcShopPolicy.BuyOutcome.InsufficientContributionPoints, result.Outcome);
        Assert.False(result.IsCleanFailure);
    }

    [Fact]
    public void Buy_ContributionPointCost_StackableItem_ScalesWithQuantity()
    {
        var npc = Shop(1, 1, 50);
        var item = Buyable(50, 2, 100, 10);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 5, null, 1, 1, 50);

        Assert.True(result.Succeeded);
        Assert.Equal(50, result.CpCost);
    }

    [Fact]
    public void Buy_RentItem_IsCleanFailure_NotDisconnect()
    {
        var npc = Shop(1, 1, 76500);
        var item = Buyable(76500, 9, 1000);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 1, 1, 0);

        Assert.Equal(NpcShopPolicy.BuyOutcome.RentItemNotPurchasable, result.Outcome);
        Assert.True(result.IsCleanFailure);
    }

    [Fact]
    public void Buy_SpecialShopNpcType_BelowMinimumLevel_IsRejected()
    {
        var npc = Shop(1, NpcShopPolicy.SpecialShopNpcType, 700);
        var item = Buyable(700, 9, 1000);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 50, 1, 0);

        Assert.Equal(NpcShopPolicy.BuyOutcome.BelowMinimumLevel, result.Outcome);
    }

    [Fact]
    public void Buy_SpecialShopNpcType_AtOrAboveMinimumLevel_Succeeds()
    {
        var npc = Shop(1, NpcShopPolicy.SpecialShopNpcType, 700);
        var item = Buyable(700, 9, 1000);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, NpcShopPolicy.SpecialShopMinimumLevel,
            1, 0);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Buy_Zone291_Applies10PercentDiscount()
    {
        var npc = Shop(1, 1, 700);
        var item = Buyable(700, 9, 1000);

        var result = NpcShopPolicy.ResolveBuy(npc, item, 0, null, 1, 291, 0);

        Assert.Equal(900, result.MoneyCost);
    }

    [Fact]
    public void Sell_RentItem_IsRejected()
    {
        var item = Sellable(76510, 9, 500);
        var result = NpcShopPolicy.ResolveSell(item, Stack(76510, 1), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Sell_CostumeItem_IsRejected()
    {
        var item = Sellable(350, 9, 500);
        var result = NpcShopPolicy.ResolveSell(item, Stack(350, 1), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(402)]
    [InlineData(18124)]
    [InlineData(18132)]
    [InlineData(93301)]
    [InlineData(93330)]
    [InlineData(93334)]
    [InlineData(93345)]
    [InlineData(93376)]
    [InlineData(93405)]
    [InlineData(76524)]
    public void Sell_CostumeItemBoundaries_AreAllRejected(int itemId)
    {
        var item = Sellable(itemId, 9, 500);
        var result = NpcShopPolicy.ResolveSell(item, Stack(itemId, 1), 0);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(93331)]
    [InlineData(93333)]
    [InlineData(93382)]
    [InlineData(93384)]
    public void Sell_CostumeGapItemIds_AreNotRejectedAsCostumes(int itemId)
    {
        var item = Sellable(itemId, 9, 500);
        var result = NpcShopPolicy.ResolveSell(item, Stack(itemId, 1), 0);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Sell_StackableRentItem_IsRejectedBeforeQuantityCheck()
    {
        var item = Sellable(76500, 2, 10);
        var result = NpcShopPolicy.ResolveSell(item, Stack(76500, 20), 5);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }
}
