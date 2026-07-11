using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Consumables;

public class HotkeyItemConsumptionResolverTests
{
    private const byte ConsumableSort = HotkeyItemConsumptionResolver.ConsumableItemCategory;

    private static HotkeyItemConsumptionResolver.Result ResolveBuffItem(int potionType1, int quantity)
    {
        var slot = new HotkeySlot(HotkeyBindingKind.Item, 12345, quantity);
        return HotkeyItemConsumptionResolver.Resolve(
            0, 0, slot,
            false, false, true,
            true, ConsumableSort, potionType1, 0,
            100, 100, 100, 100);
    }

    [Fact]
    public void AssassinScroll_PotionType12_WritesSlot15_Value3_Duration40Seconds()
    {
        var result = ResolveBuffItem(12, 3);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(15, write.Slot);
        Assert.Equal(3, write.Value);
        Assert.Equal(SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(40)), write.DurationTicks);
        Assert.Equal(80, write.DurationTicks);
    }

    [Fact]
    public void DepartedSpiritScroll_PotionType13_WritesSlot15_Value3_Duration60Seconds()
    {
        var result = ResolveBuffItem(13, 5);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.Success, result.Outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Buff, result.Effect);
        var write = Assert.Single(result.BuffWrites);
        Assert.Equal(15, write.Slot);
        Assert.Equal(3, write.Value);
        Assert.Equal(120, write.DurationTicks);
    }

    [Fact]
    public void AttackIncreaseBook_PotionType14_WritesSlot17_Value25_Duration60Seconds()
    {
        var result = ResolveBuffItem(14, 1);

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
        var result = ResolveBuffItem(15, 1);

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
            50, 100, 100, 100);

        Assert.Equal(HotkeyItemConsumptionResolver.EffectKind.Life, result.Effect);
        Assert.Empty(result.BuffWrites);
    }

        [Theory]
    [InlineData(6)]
    [InlineData(16)]
    public void StillUnwiredPotionTypes_AreCleanlyRejected_NotTreatedAsABuff(int potionType1)
    {
        var result = ResolveBuffItem(potionType1, 1);

        Assert.Equal(HotkeyItemConsumptionResolver.Outcome.RejectedClean, result.Outcome);
        Assert.Empty(result.BuffWrites);
    }
}
