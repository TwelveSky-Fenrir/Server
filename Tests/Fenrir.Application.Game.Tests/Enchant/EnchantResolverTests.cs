using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

public class EnchantResolverTests
{
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
    public void EquipSortOutOfRange_IsRejected()
    {
        var target = Equip(1, 5);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void CheckImproveNotTwo_IsRejected()
    {
        var target = Equip(1, checkImprove: 0);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AlreadyAtMaxImprove_IsRejected()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(50), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void WingTarget_UsesSameMachineryAsEquipment_SucceedsAndFlagsIsWing()
    {
        var target = Equip(1, 6);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(1, result.NewEnchant);
        Assert.Equal(10000, result.Cost);
        Assert.True(result.IsWing);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void NonWingEquipTarget_IsWingFalse()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.False(result.IsWing);
    }

    [Fact]
    public void UnrecognizedMaterial_IsRejected()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(999999), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void StoneMaterial_SuccessRoll_IncrementsEnchantByBaseValue()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(1, result.NewEnchant);
        Assert.Equal(10000, result.Cost);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_FailureBelowSafeValue_NeverDestroys()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(10), Material(1019), 0, 0,
            0, new ScriptedRandomSource(99,
                0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Failed, result.Outcome);
        Assert.Equal(9, result.NewEnchant);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_FailureAboveSafeValue_DestroyRollFails_IsPlainFailure()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(25), Material(1019), 0, 0,
            0, new ScriptedRandomSource(99, 50));

        Assert.Equal(EnchantResolver.EnchantOutcome.Failed, result.Outcome);
        Assert.Equal(24, result.NewEnchant);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_DestroyRollHits_NoProtection_ItemDestroyed()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(25), Material(1019), 0, 0,
            0, new ScriptedRandomSource(99, 0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Destroyed, result.Outcome);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_DestroyRollHits_ProtectionConsumed_EnchantOnlyDecrements()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(25), Material(1019), 0, 1,
            0, new ScriptedRandomSource(99, 0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Protected, result.Outcome);
        Assert.Equal(24, result.NewEnchant);
        Assert.True(result.ConsumesProtectCharge);
    }

    [Fact]
    public void StoneMaterial_FailureAtZero_NeverGoesNegative()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1023), 0, 0,
            0, new ScriptedRandomSource(90));

        Assert.Equal(EnchantResolver.EnchantOutcome.Failed, result.Outcome);
        Assert.Equal(0, result.NewEnchant);
    }

    [Fact]
    public void StoneMaterial_NearFortyCap_ClampsIncrementToBoundary()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(38), Material(1023), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    [Fact]
    public void ForcedScroll633_AlwaysSucceeds_EvenOnWorstRoll_NoTypeRequirement()
    {
        var target = Equip(1, type: 0);
        var result = EnchantResolver.Resolve(target, Target(5), Material(633), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(6, result.NewEnchant);
        Assert.Equal(0, result.Cost);
    }

    [Fact]
    public void FillToValueScroll619_RequiresRareOrElite_RejectsOtherwise()
    {
        var target = Equip(1, type: 0);
        var result = EnchantResolver.Resolve(target, Target(5), Material(619), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void FillToValueScroll619_JumpsStraightToFortyFromAnyLevel()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(12), Material(619), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    [Fact]
    public void FillToValueScroll540_RequiresBelowThirty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(30), Material(540), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Scroll825_IgnoresFortyCap_JumpsStraightToFiftyFromBelowForty()
    {
        var target = Equip(1, type: 4);
        var result = EnchantResolver.Resolve(target, Target(3), Material(825), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(50, result.NewEnchant);
        Assert.Equal(0, result.Cost);
    }

    [Fact]
    public void AtExactlyForty_RequiresUnsealMaterial()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(40), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AtExactlyForty_UnsealMaterial_AlwaysSucceeds_NoRoll()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(40), Material(EnchantMaterialCatalog.UnsealItemId),
            0, 0, 0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Unsealed, result.Outcome);
        Assert.Equal(41, result.NewEnchant);
        Assert.Equal(0, result.Cost);
    }

    [Fact]
    public void AdvancedRegime_RequiresRareOrElite()
    {
        var target = Equip(1, type: 0);
        var result = EnchantResolver.Resolve(target, Target(41), Material(1023), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AdvancedRegime_Tier41To43_20PercentRate_SuccessBelowThreshold()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(41), Material(1023), 0, 0,
            0, new ScriptedRandomSource(19));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(42, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_Tier41To43_FailAtExactly41_ResetsToForty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(41), Material(1023), 0, 0,
            0, new ScriptedRandomSource(20));

        Assert.Equal(EnchantResolver.EnchantOutcome.ResetToForty, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_FailAbove41_NoProtection_ResetsToForty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(45), Material(1023), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.ResetToForty, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_FailAbove41_WithProtection_OnlyDecrementsByOne()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(45), Material(1023), 0, 1,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Protected, result.Outcome);
        Assert.Equal(44, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_NeverDestroysTheItem()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(49), Material(1023), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.NotEqual(EnchantResolver.EnchantOutcome.Destroyed, result.Outcome);
    }

    [Fact]
    public void AdvancedRegime_ForcedScroll825_TenPointJump_ClampedToFifty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(45), Material(825), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(50, result.NewEnchant);
    }

    [Fact]
    public void ImproveCharge_Present_ConsumedOnRolledSuccess()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            1, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.True(result.ConsumesImproveCharge);
    }

    [Fact]
    public void ImproveCharge_Present_ConsumedOnRolledFailure()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1023), 0, 0,
            1, new ScriptedRandomSource(90));

        Assert.Equal(EnchantResolver.EnchantOutcome.Failed, result.Outcome);
        Assert.True(result.ConsumesImproveCharge);
    }

    [Fact]
    public void ImproveCharge_Absent_NeverConsumed()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.False(result.ConsumesImproveCharge);
    }

    [Fact]
    public void ImproveCharge_Present_NotConsumedOnNoRollUnseal()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(40), Material(EnchantMaterialCatalog.UnsealItemId),
            0, 0, 1, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Unsealed, result.Outcome);
        Assert.False(result.ConsumesImproveCharge);
    }

    [Fact]
    public void ImproveCharge_Present_ConsumedInAdvancedRegime()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(41), Material(1023), 0, 0,
            1, new ScriptedRandomSource(19));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.True(result.ConsumesImproveCharge);
    }
}
