using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

public class EnchantResolverWingProtectedMaterialTests
{
    private const int ProtectedMaterialId = WingEnchantMaterialWhitelist.ProtectedMaterialItemId;

    private static ItemDefinition Equip(int itemId, byte sort = 6, byte type = 0, byte checkImprove = 2)
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
    public void SuccessRoll_IncrementsByOne_CostsFlatFiftyCp_FlagsIsWing()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(0), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(1, result.NewEnchant);
        Assert.Equal(WingEnchantMaterialWhitelist.WingEnchantCpCost, result.Cost);
        Assert.True(result.IsWing);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void FailureRoll_BelowSafeValue_LeavesEnchantUntouched_ChargesFlatCp()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(10), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(10, result.NewEnchant);
        Assert.Equal(WingEnchantMaterialWhitelist.WingEnchantCpCost, result.Cost);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void FailureRoll_AboveSafeValue_NeverDestroys_LeavesEnchantUntouched()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(30), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(30, result.NewEnchant);
    }

    [Fact]
    public void ImproveCharge_ConsumedOnRolledSuccessAndFailure()
    {
        var success = EnchantResolver.Resolve(Equip(1), Target(0), Material(ProtectedMaterialId), 0, 0,
            1, new ScriptedRandomSource(0));
        Assert.True(success.ConsumesImproveCharge);

        var failure = EnchantResolver.Resolve(Equip(1), Target(10), Material(ProtectedMaterialId), 0, 0,
            1, new ScriptedRandomSource(99));
        Assert.True(failure.ConsumesImproveCharge);
    }

    [Fact]
    public void NonWingTarget_IsRejected_MaterialIsWingExclusive()
    {
        var result = EnchantResolver.Resolve(Equip(1, sort: 7), Target(0), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void SiblingMaterial695_FailurePathUnresolved_StaysRejected()
    {
        var result = EnchantResolver.Resolve(Equip(1),
            Target(0), Material(WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }
}
