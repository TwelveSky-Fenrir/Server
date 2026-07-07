using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Consumables;

/// <summary>
///     Covers <see cref="HotkeyItemConsumptionResolver" />'s potion-type 12-15 self-buff branches (Assassin
///     Scroll/Departed Spirit Scroll/Attack Increase Book/Dodge Increase Book, world.Items
///     1364/1156/1471/1472) added on top of its pre-existing life/mana/no-op coverage.
/// </summary>
public class HotkeyItemConsumptionResolverTests
{
    private const byte ConsumableSort = HotkeyItemConsumptionResolver.ConsumableItemCategory;

    private static HotkeyItemConsumptionResolver.Result ResolveBuffItem(int potionType1, int quantity)
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Item, 12345, quantity);
        return HotkeyItemConsumptionResolver.Resolve(
            page: 0, index: 0, slot,
            isStunned: false, isDead: false, canUseConsumables: true,
            itemResolved: true, itemCategory: ConsumableSort, potionType1: potionType1, potionType2: 0,
            life: 100, effectiveMaxLife: 100, mana: 100, effectiveMaxMana: 100);
    }

    [Fact]
    public void AssassinScroll_PotionType12_WritesSlot15_Value3_Duration40Seconds()
    {
        var result = ResolveBuffItem(potionType1: 12, quantity: 3);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(15, write.Slot);
        Assert.Equal(3, write.Value);
        Assert.Equal(SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(40)), write.DurationTicks);
        Assert.Equal(80, write.DurationTicks); // 40s @ the shipped 500ms legacy tick
    }

    [Fact]
    public void DepartedSpiritScroll_PotionType13_WritesSlot15_Value3_Duration60Seconds()
    {
        var result = ResolveBuffItem(potionType1: 13, quantity: 5);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(15, write.Slot);
        Assert.Equal(3, write.Value);
        Assert.Equal(120, write.DurationTicks); // 60s @ the shipped 500ms legacy tick
    }

    [Fact]
    public void AttackIncreaseBook_PotionType14_WritesSlot17_Value25_Duration60Seconds()
    {
        var result = ResolveBuffItem(potionType1: 14, quantity: 1);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(17, write.Slot);
        Assert.Equal(25, write.Value);
        Assert.Equal(120, write.DurationTicks);
    }

    [Fact]
    public void DodgeIncreaseBook_PotionType15_WritesSlot18_Value25_Duration60Seconds()
    {
        var result = ResolveBuffItem(potionType1: 15, quantity: 1);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(18, write.Slot);
        Assert.Equal(25, write.Value);
        Assert.Equal(120, write.DurationTicks);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void BuffItem_LastUnit_ClearsTheSlotInsteadOfDecrementing(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, quantity: 1);

        Assert.True(result.NewSlot.IsEmpty);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void BuffItem_MultipleUnits_DecrementsQuantityByOne_AndKeepsTheBinding(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, quantity: 4);

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
        var result = ResolveBuffItem(potionType1, quantity: 1);

        Assert.Equal(0, result.LifeGain);
        Assert.Equal(0, result.ManaGain);
    }

    /// <summary>Regression guard: every non-buff EffectKind still reports an empty BuffWrites array.</summary>
    [Fact]
    public void FlatLifeGain_PotionType1_ReportsNoBuffWrites()
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Item, 2, 1);
        var result = HotkeyItemConsumptionResolver.Resolve(
            page: 0, index: 0, slot,
            isStunned: false, isDead: false, canUseConsumables: true,
            itemResolved: true, itemCategory: ConsumableSort, potionType1: 1, potionType2: 30,
            life: 50, effectiveMaxLife: 100, mana: 100, effectiveMaxMana: 100);

        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Life, result.Effect);
        Assert.Empty(result.BuffWrites);
    }

    /// <summary>Regression guard: pet-activity (6) and mount-activity (16) remain a clean reject, not a buff write.</summary>
    [Theory]
    [InlineData(6)]
    [InlineData(16)]
    public void StillUnwiredPotionTypes_AreCleanlyRejected_NotTreatedAsABuff(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, quantity: 1);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Empty(result.BuffWrites);
    }
}
