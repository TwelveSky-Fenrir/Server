using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.World.Npcs;

/// <summary>
///     Coverage of the NPC-sell WarPoint-adjacent exemption added to <see cref="NpcShopPolicy.ResolveSell" />:
///     the live <c>99703</c>-<c>99756</c> range (<c>#elif defined LNW33</c>,
///     <c>Server/ts25zone/S04_MyWork05.cpp:1496-1522</c>) skips the rare/costume sell-block and sells for the
///     sell price. The dead <c>74200</c>-<c>74223</c> (<c>#ifdef USE_CUSTOME_CREATE</c>) range is deliberately
///     not carried. Complements <c>NpcShopPolicyTests</c>, which covers the non-exempt sell/buy resolution.
/// </summary>
public class NpcShopPolicySellExemptTests
{
    private const byte NonStackableSort = 9;
    private const byte RareType = 3; // IRARE (STRUCT.h:1657)

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
        return new ItemStack(itemId, 1, 5, 0, 0, 0, 0, 0, 0, 0, 0); // Enchant != 0 -> normally rare-blocked
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
    [InlineData(74200)] // dead USE_CUSTOME_CREATE range -- must NOT be treated as exempt
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
        // Control: the same rare/enchanted item just outside the exempt range is blocked as before.
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
        // The exemption is scoped to the rare/costume blocks; the earlier CheckNpcSell==1 gate still rejects.
        var result = NpcShopPolicy.ResolveSell(Item(99720, RareType, checkNpcSell: 1), Enchanted(99720), 0);

        Assert.False(result.Succeeded);
        Assert.Equal(NpcShopPolicy.SellOutcome.Rejected, result.Outcome);
    }
}
