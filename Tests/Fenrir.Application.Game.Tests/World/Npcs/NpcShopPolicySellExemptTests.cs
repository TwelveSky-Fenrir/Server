using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.World.Npcs;

public class NpcShopPolicySellExemptTests
{
    private const byte NonStackableSort = 9;
    private const byte RareType = 3;

    private static ItemDefinition Item(int itemId, byte type = 0, int sellCost = 500, byte checkNpcSell = 0)
    {
        var row = WorldDataTestRows.Item(itemId) with
        {
            Sort = NonStackableSort, Type = type, SellCost = sellCost, CheckNpcSell = checkNpcSell
        };
        return new ItemDefinition(row, []);
    }

    private static ItemStack Enchanted(int itemId)
    {
        return new ItemStack(itemId, 1, 5, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Theory]
    [InlineData(99703)]
    [InlineData(99756)]
    public void IsSellExempt_TrueAtBothEndsOfTheLiveRange(int itemId)
    {
        Assert.True(NpcShopPolicy.IsSellExempt(itemId));
    }

    [Theory]
    [InlineData(99702)]
    [InlineData(99757)]
    [InlineData(74200)]
    [InlineData(74223)]
    public void IsSellExempt_FalseOutsideTheLiveRange(int itemId)
    {
        Assert.False(NpcShopPolicy.IsSellExempt(itemId));
    }

    [Fact]
    public void ExemptRareItem_SkipsRareBlock_SellsForSellCost()
    {
        var result = NpcShopPolicy.ResolveSell(Item(99720, RareType), Enchanted(99720), 0);

        Assert.True(result.Succeeded);
        Assert.Equal(500, result.MoneyGained);
        Assert.Null(result.RemainingSourceStack);
    }

    [Fact]
    public void NonExemptRareEnchantedItem_IsStillRejected()
    {
        var result = NpcShopPolicy.ResolveSell(Item(99702, RareType), Enchanted(99702), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void DeadUseCustomCreateRangeItem_IsNotExempt_StillRejectedWhenRareEnchanted()
    {
        var result = NpcShopPolicy.ResolveSell(Item(74210, RareType), Enchanted(74210), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void ExemptItem_DoesNotOverrideCheckNpcSellBlock()
    {
        var result = NpcShopPolicy.ResolveSell(Item(99720, RareType, checkNpcSell: 1), Enchanted(99720), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }
}
