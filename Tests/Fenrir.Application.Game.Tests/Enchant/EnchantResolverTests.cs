using Fenrir.Application.Game.Enchant;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
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

    // ---- Structural rejections ----

    [Fact]
    public void EquipSortOutOfRange_IsRejected()
    {
        var target = Equip(1, sort: 5);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void CheckImproveNotTwo_IsRejected()
    {
        var target = Equip(1, checkImprove: 0);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AlreadyAtMaxImprove_IsRejected()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(50), Material(1019), 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void WingTarget_IsNotSupported_NotRejected()
    {
        var target = Equip(1, sort: 6);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.NotSupported, result.Outcome);
        Assert.False(result.ConsumesMaterial);
    }

    [Fact]
    public void UnrecognizedMaterial_IsRejected()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(999999), 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    // ---- Standard regime (+0..+40), stone materials -- real probabilistic path ----

    [Fact]
    public void StoneMaterial_SuccessRoll_IncrementsEnchantByBaseValue()
    {
        var target = Equip(1);
        // p1 = 103 - 3*1 + 0 = 100 -- rolling 0 (< 100) always succeeds at +0 -> +1.
        var result = EnchantResolver.Resolve(target, Target(0), Material(1019), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(1, result.NewEnchant);
        Assert.Equal(10000, result.Cost);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_FailureBelowSafeValue_NeverDestroys()
    {
        // current=10, +1019(+1) => new=11 <= SAFE_IMPROVE_VALUE(20): destroy roll never happens even on
        // a guaranteed-fail first roll.
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(10), Material(1019), luck: 0, 0,
            new ScriptedRandomSource(99, 0)); // fail the success roll (99 >= p1), then would-be destroy roll unreachable

        Assert.Equal(EnchantResolver.EnchantOutcome.Failed, result.Outcome);
        Assert.Equal(9, result.NewEnchant);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_FailureAboveSafeValue_DestroyRollFails_IsPlainFailure()
    {
        // current=25, +1 => new=26 > 20: destroy roll happens. p2 = -57+26*3-0 = 21. Roll 50 (>=21) misses
        // the destroy, falls through to plain failure.
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(25), Material(1019), luck: 0, 0,
            new ScriptedRandomSource(99, 50));

        Assert.Equal(EnchantResolver.EnchantOutcome.Failed, result.Outcome);
        Assert.Equal(24, result.NewEnchant);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_DestroyRollHits_NoProtection_ItemDestroyed()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(25), Material(1019), luck: 0, protectForDestroyCharges: 0,
            new ScriptedRandomSource(99, 0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Destroyed, result.Outcome);
    }

    [Fact]
    public void StoneMaterial_HighEnchant_DestroyRollHits_ProtectionConsumed_EnchantOnlyDecrements()
    {
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(25), Material(1019), luck: 0, protectForDestroyCharges: 1,
            new ScriptedRandomSource(99, 0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Protected, result.Outcome);
        Assert.Equal(24, result.NewEnchant);
        Assert.True(result.ConsumesProtectCharge);
    }

    [Fact]
    public void StoneMaterial_FailureAtZero_NeverGoesNegative()
    {
        // current=0, material 1023 (+5) -> newImprove=5, p1=103-15+0=88 (NOT the guaranteed-success 100 a
        // +1 stone would give at this level) -- roll 90 (>= 88) fails; current+value=5 <= SAFE_IMPROVE_VALUE
        // so no destroy roll is even reachable, straight to plain failure. Enchant was already 0, so it must
        // floor there rather than go negative.
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(0), Material(1023), luck: 0, 0,
            new ScriptedRandomSource(90));

        Assert.Equal(EnchantResolver.EnchantOutcome.Failed, result.Outcome);
        Assert.Equal(0, result.NewEnchant);
    }

    [Fact]
    public void StoneMaterial_NearFortyCap_ClampsIncrementToBoundary()
    {
        // current=38, material 1023 (+5) would reach 43 -- clamped to 40.
        var target = Equip(1);
        var result = EnchantResolver.Resolve(target, Target(38), Material(1023), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    // ---- Forced-100% scrolls (633/619/540/538/551/565/825) ----

    [Fact]
    public void ForcedScroll633_AlwaysSucceeds_EvenOnWorstRoll_NoTypeRequirement()
    {
        var target = Equip(1, type: 0);
        var result = EnchantResolver.Resolve(target, Target(5), Material(633), luck: 0, 0,
            new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(6, result.NewEnchant);
        Assert.Equal(0, result.Cost);
    }

    [Fact]
    public void FillToValueScroll619_RequiresRareOrElite_RejectsOtherwise()
    {
        var target = Equip(1, type: 0); // neither Rare(3) nor Elite(4)
        var result = EnchantResolver.Resolve(target, Target(5), Material(619), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void FillToValueScroll619_JumpsStraightToFortyFromAnyLevel()
    {
        var target = Equip(1, type: 3); // IRARE
        var result = EnchantResolver.Resolve(target, Target(12), Material(619), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    [Fact]
    public void FillToValueScroll540_RequiresBelowThirty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(30), Material(540), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Scroll825_IgnoresFortyCap_JumpsStraightToFiftyFromBelowForty()
    {
        var target = Equip(1, type: 4); // IELITE
        var result = EnchantResolver.Resolve(target, Target(3), Material(825), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(50, result.NewEnchant);
        Assert.Equal(0, result.Cost);
    }

    // ---- Advanced regime (+41..+50) ----

    [Fact]
    public void AtExactlyForty_RequiresUnsealMaterial()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(40), Material(1019), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AtExactlyForty_UnsealMaterial_AlwaysSucceeds_NoRoll()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(40), Material(EnchantMaterialCatalog.UnsealItemId),
            luck: 0, 0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Unsealed, result.Outcome);
        Assert.Equal(41, result.NewEnchant);
        Assert.Equal(0, result.Cost);
    }

    [Fact]
    public void AdvancedRegime_RequiresRareOrElite()
    {
        var target = Equip(1, type: 0);
        var result = EnchantResolver.Resolve(target, Target(41), Material(1023), luck: 0, 0,
            new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void AdvancedRegime_Tier41To43_20PercentRate_SuccessBelowThreshold()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(41), Material(1023), luck: 0, 0,
            new ScriptedRandomSource(19));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(42, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_Tier41To43_FailAtExactly41_ResetsToForty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(41), Material(1023), luck: 0, 0,
            new ScriptedRandomSource(20));

        Assert.Equal(EnchantResolver.EnchantOutcome.ResetToForty, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_FailAbove41_NoProtection_ResetsToForty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(45), Material(1023), luck: 0, protectForDestroyCharges: 0,
            new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.ResetToForty, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_FailAbove41_WithProtection_OnlyDecrementsByOne()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(45), Material(1023), luck: 0, protectForDestroyCharges: 1,
            new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Protected, result.Outcome);
        Assert.Equal(44, result.NewEnchant);
    }

    [Fact]
    public void AdvancedRegime_NeverDestroysTheItem()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(49), Material(1023), luck: 0, 0,
            new ScriptedRandomSource(99));

        Assert.NotEqual(EnchantResolver.EnchantOutcome.Destroyed, result.Outcome);
    }

    [Fact]
    public void AdvancedRegime_ForcedScroll825_TenPointJump_ClampedToFifty()
    {
        var target = Equip(1, type: 3);
        var result = EnchantResolver.Resolve(target, Target(45), Material(825), luck: 0, 0,
            new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(50, result.NewEnchant);
    }
}
