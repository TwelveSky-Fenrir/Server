using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

/// <summary>
///     The special "no-change" material 8101 (Server/ts25zone/S04_MyWork02.cpp:3315-3318,3370-3378): destroy
///     chance forced to 0, and a failed success roll leaves the enchant untouched (ZC result 8) instead of
///     downgrading or destroying.
/// </summary>
public class EnchantResolverNoChangeTests
{
    private const int NoChangeMaterialId = 8101;
    private const int NoChangeMaterialMoneyCost = 10_000_000;

    private static ItemDefinition Equip(int itemId, byte sort = 7, byte type = 0, byte checkImprove = 2)
    {
        var row = WorldDataTestRows.Item(itemId) with { Sort = sort, Type = type, CheckImprove = checkImprove };
        return new ItemDefinition(row, []);
    }

    private static ItemDefinition Material(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId), []);
    }

    private static ItemStack Target(byte enchant)
    {
        return new ItemStack(1, 1, enchant, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void NoChangeMaterial_SuccessRoll_BehavesAsAnOrdinaryPlusOne()
    {
        // current=10, +1 => new=11, p1 = 103 - 33 = 70; roll 0 succeeds.
        var result = EnchantResolver.Resolve(Equip(1), Target(10), Material(NoChangeMaterialId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(11, result.NewEnchant);
        Assert.Equal(NoChangeMaterialMoneyCost, result.Cost);
    }

    [Fact]
    public void NoChangeMaterial_FailureBelowSafeValue_LeavesEnchantUntouched()
    {
        // current=10, +1 => new=11 (<= safe 20); roll 99 fails the success roll. An ordinary stone would
        // downgrade to 9 -- 8101 leaves it at 10.
        var result = EnchantResolver.Resolve(Equip(1), Target(10), Material(NoChangeMaterialId), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(10, result.NewEnchant);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void NoChangeMaterial_FailureAboveSafeValue_NeverDestroys_LeavesEnchantUntouched()
    {
        // current=25, +1 => new=26 (> safe 20): an ordinary stone would roll for destruction. 8101 short-
        // circuits before the destroy block, so a single failing roll yields NoChange, never Destroyed.
        var result = EnchantResolver.Resolve(Equip(1), Target(25), Material(NoChangeMaterialId), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(25, result.NewEnchant);
    }

    [Fact]
    public void NoChangeMaterial_Failure_ConsumesSweetPotatoChargeLikeAnyRolledAttempt()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(10), Material(NoChangeMaterialId), 0, 0,
            1, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.True(result.ConsumesImproveCharge);
    }
}
