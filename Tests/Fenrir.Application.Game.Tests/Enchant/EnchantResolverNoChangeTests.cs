using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

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
        var result = EnchantResolver.Resolve(Equip(1), Target(10), Material(NoChangeMaterialId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(11, result.NewEnchant);
        Assert.Equal(NoChangeMaterialMoneyCost, result.Cost);
    }

    [Fact]
    public void NoChangeMaterial_FailureBelowSafeValue_LeavesEnchantUntouched()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(10), Material(NoChangeMaterialId), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(10, result.NewEnchant);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void NoChangeMaterial_FailureAboveSafeValue_NeverDestroys_LeavesEnchantUntouched()
    {
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
