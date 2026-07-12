using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;

namespace Fenrir.Application.Game.Tests.Consumables;

public class HotkeyItemConsumptionResolverTests
{
    private const byte ConsumableSort = HotkeyItemConsumptionResolver.ConsumableItemCategory;

    private static HotkeyItemConsumptionResolver.Result ResolveBuffItem(int potionType1, int quantity,
        bool isZoneAllowed = true, int currentDarkAttackMarker = 0)
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Item, 12345, quantity);
        return HotkeyItemConsumptionResolver.Resolve(
            0, 0, slot,
            false, false, true,
            true, ConsumableSort, potionType1, 0,
            100, 100, 100, 100,
            isZoneAllowed, currentDarkAttackMarker,
            false, 0, false, 0);
    }

    private static HotkeyItemConsumptionResolver.Result ResolveFoodItem(int potionType1, int potionType2,
        int quantity, bool petFoodEligible = false, int petActivity = 0, bool mountFoodEligible = false,
        int mountActivity = 0)
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Item, 12345, quantity);
        return HotkeyItemConsumptionResolver.Resolve(
            0, 0, slot,
            false, false, true,
            true, ConsumableSort, potionType1, potionType2,
            100, 100, 100, 100,
            true, 0,
            petFoodEligible, petActivity, mountFoodEligible, mountActivity);
    }

    [Fact]
    public void AssassinScroll_PotionType12_WritesSlot15_Value3_Duration40LegacyTicks()
    {
        var result = ResolveBuffItem(12, 3);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(15, write.Slot);
        Assert.Equal(3, write.Value);
        Assert.Equal(40, write.DurationTicks);
    }

    [Fact]
    public void DepartedSpiritScroll_PotionType13_WritesSlot15_Value3_Duration60LegacyTicks()
    {
        var result = ResolveBuffItem(13, 5);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(15, write.Slot);
        Assert.Equal(3, write.Value);
        Assert.Equal(60, write.DurationTicks);
    }

    [Fact]
    public void AttackIncreaseBook_PotionType14_WritesSlot17_Value25_Duration60LegacyTicks()
    {
        var result = ResolveBuffItem(14, 1);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(17, write.Slot);
        Assert.Equal(25, write.Value);
        Assert.Equal(60, write.DurationTicks);
    }

    [Fact]
    public void DodgeIncreaseBook_PotionType15_WritesSlot18_Value25_Duration60LegacyTicks()
    {
        var result = ResolveBuffItem(15, 1);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(18, write.Slot);
        Assert.Equal(25, write.Value);
        Assert.Equal(60, write.DurationTicks);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void BuffItem_LastUnit_ClearsTheSlotInsteadOfDecrementing(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 1);

        Assert.True(result.NewSlot.IsEmpty);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void BuffItem_MultipleUnits_DecrementsQuantityByOne_AndKeepsTheBinding(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 4);

        Assert.False(result.NewSlot.IsEmpty);
        Assert.Equal(HotkeyBindingKind.Item, result.NewSlot.Kind);
        Assert.Equal(12345, result.NewSlot.Value1);
        Assert.Equal(3, result.NewSlot.Value2);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void BuffItem_DoesNotGrantLifeOrMana(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 1);

        Assert.Equal(0, result.LifeGain);
        Assert.Equal(0, result.ManaGain);
    }

    [Fact]
    public void FlatLifeGain_PotionType1_ReportsNoBuffWrites()
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Item, 2, 1);
        var result = HotkeyItemConsumptionResolver.Resolve(
            0, 0, slot,
            false, false, true,
            true, ConsumableSort, 1, 30,
            50, 100, 100, 100,
            true, 0,
            false, 0, false, 0);

        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Life, result.Effect);
        Assert.Empty(result.BuffWrites);
    }

    [Fact]
    public void PetFood_PotionType6_NoPetEquipped_RejectedCleanly()
    {
        var result = ResolveFoodItem(6, 20, 1, petFoodEligible: false, petActivity: 10);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Equal(0, result.PetActivityGain);
    }

    [Fact]
    public void PetFood_PotionType6_AlreadyAtActivityCap_RejectedCleanly_BeforeConsumption()
    {
        var result = ResolveFoodItem(6, 20, 3, petFoodEligible: true, petActivity: 100);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Equal(3, result.NewSlot.Value2);
    }

    [Fact]
    public void PetFood_PotionType6_AmountWouldOvershootCap_ClampsToRemainingHeadroom()
    {
        var result = ResolveFoodItem(6, 5, 1, petFoodEligible: true, petActivity: 99);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(1, result.PetActivityGain);
    }

    [Fact]
    public void PetFood_PotionType6_NonPositiveDefinedAmount_RejectedCleanly()
    {
        var result = ResolveFoodItem(6, 0, 1, petFoodEligible: true, petActivity: 10);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
    }

    [Fact]
    public void PetFood_PotionType6_Eligible_GrantsTheFullDefinedAmount_AndDecrementsStack()
    {
        var result = ResolveFoodItem(6, 20, 3, petFoodEligible: true, petActivity: 50);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.PetActivity, result.Effect);
        Assert.Equal(20, result.PetActivityGain);
        Assert.Equal(0, result.MountActivityGain);
        Assert.Equal(2, result.NewSlot.Value2);
    }

    [Fact]
    public void MountFood_PotionType16_NoAnimalMounted_RejectedCleanly()
    {
        var result = ResolveFoodItem(16, 20, 1, mountFoodEligible: false, mountActivity: 10);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Equal(0, result.MountActivityGain);
    }

    [Fact]
    public void MountFood_PotionType16_AmountWouldOvershootCap_ClampsToRemainingHeadroom()
    {
        var result = ResolveFoodItem(16, 5, 1, mountFoodEligible: true, mountActivity: 99);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(1, result.MountActivityGain);
    }

    [Fact]
    public void MountFood_PotionType16_Eligible_GrantsTheFullDefinedAmount_AndDecrementsStack()
    {
        var result = ResolveFoodItem(16, 15, 2, mountFoodEligible: true, mountActivity: 40);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.MountActivity, result.Effect);
        Assert.Equal(15, result.MountActivityGain);
        Assert.Equal(0, result.PetActivityGain);
        Assert.True(result.NewSlot.IsEmpty);
    }

    [Fact]
    public void MountFood_PotionType16_ExactlyFillsToCap_StillSucceeds()
    {
        var result = ResolveFoodItem(16, 40, 1, mountFoodEligible: true, mountActivity: 60);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(40, result.MountActivityGain);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    public void DarkAttackScroll_OutsideZoneAllowlist_RejectedCleanly_NoStateChange(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 1, isZoneAllowed: false);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Empty(result.BuffWrites);
    }

    [Fact]
    public void AssassinScroll_WhileDepartedSpiritScrollActive_RejectedCleanly()
    {
        var result = ResolveBuffItem(12, 1,
            currentDarkAttackMarker: HotkeyItemConsumptionResolver.DepartedSpiritScrollMarkerValue);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Empty(result.BuffWrites);
    }

    [Fact]
    public void DepartedSpiritScroll_WhileAssassinScrollActive_RejectedCleanly()
    {
        var result = ResolveBuffItem(13, 1,
            currentDarkAttackMarker: HotkeyItemConsumptionResolver.AssassinScrollMarkerValue);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Empty(result.BuffWrites);
    }

    [Fact]
    public void AssassinScroll_WhileAssassinScrollAlreadyActive_RefreshesInsteadOfBeingRejected()
    {
        var result = ResolveBuffItem(12, 1,
            currentDarkAttackMarker: HotkeyItemConsumptionResolver.AssassinScrollMarkerValue);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.AntiCheatMarkerKind.DarkAttack, result.MarkerKind);
        Assert.Equal(HotkeyItemConsumptionResolver.AssassinScrollMarkerValue, result.MarkerValue);
    }

    [Fact]
    public void DepartedSpiritScroll_WhileDepartedSpiritScrollAlreadyActive_RefreshesInsteadOfBeingRejected()
    {
        var result = ResolveBuffItem(13, 1,
            currentDarkAttackMarker: HotkeyItemConsumptionResolver.DepartedSpiritScrollMarkerValue);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.AntiCheatMarkerKind.DarkAttack, result.MarkerKind);
        Assert.Equal(HotkeyItemConsumptionResolver.DepartedSpiritScrollMarkerValue, result.MarkerValue);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(15)]
    public void AttackOrDodgeBook_IgnoresZoneAllowlistAndDarkAttackMarker(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 1, isZoneAllowed: false,
            currentDarkAttackMarker: HotkeyItemConsumptionResolver.AssassinScrollMarkerValue);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    public void DarkAttackScroll_NeverTriggersStatRecompute(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 1);

        Assert.False(result.RecomputeStats);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(15)]
    public void AttackOrDodgeBook_AlwaysTriggersStatRecompute(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 1);

        Assert.True(result.RecomputeStats);
    }

    [Fact]
    public void AttackIncreaseBook_SetsHitRateMarker_NotDarkAttackMarker()
    {
        var result = ResolveBuffItem(14, 1);

        Assert.Equal(HotkeyItemConsumptionResolver.AntiCheatMarkerKind.HitRate, result.MarkerKind);
        Assert.Equal(HotkeyItemConsumptionResolver.HitOrDodgeBuffMarkerValue, result.MarkerValue);
    }

    [Fact]
    public void DodgeIncreaseBook_SetsDodgeRateMarker_NotDarkAttackMarker()
    {
        var result = ResolveBuffItem(15, 1);

        Assert.Equal(HotkeyItemConsumptionResolver.AntiCheatMarkerKind.DodgeRate, result.MarkerKind);
        Assert.Equal(HotkeyItemConsumptionResolver.HitOrDodgeBuffMarkerValue, result.MarkerValue);
    }
}
