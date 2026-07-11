using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

public class FourGuildElixirContributionTests
{
    private static readonly ZoneContext EligibleZone = new(1);


    [Fact]
    public void AllFamilies_DefaultOverrides_ReproduceB2RawFloor()
    {
        var life = new ConsumableContext(50);
        var mana = new ConsumableContext(EatManaPotion: 50);
        var str = new ConsumableContext(EatStrPotion: 50);
        var dex = new ConsumableContext(EatDexPotion: 50);

        Assert.Equal(20 * 50, StatCalculator.LifeElixirContributionWithOverride(life, EligibleZone));
        Assert.Equal(25 * 50, StatCalculator.ManaElixirContributionWithOverride(mana, EligibleZone));
        Assert.Equal(3 * 50, StatCalculator.StrengthElixirAttackContributionWithOverride(str, EligibleZone));
        Assert.Equal(2 * 50, StatCalculator.AccuracyElixirContributionWithOverride(dex, EligibleZone));
        Assert.Equal(2 * 50, StatCalculator.BlockElixirContributionWithOverride(dex, EligibleZone));
    }

    [Fact]
    public void LifeRawFloor_HoldsInReEnableBandAndNormalZone()
    {
        var consumable = new ConsumableContext(10);

        foreach (var zoneNumber in (short[])[1, 319, 320, 323])
        {
            var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, new ZoneContext(zoneNumber));
            Assert.Equal(20 * 10, delta);
        }
    }


    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(0, 0, false)]
    [InlineData(-1, 1, false)]
    public void HpFixedOverride_ArmsForSlotsZeroAndOne_WithFlagSet(int slot, int flag, bool expectFixed)
    {
        var consumable = new ConsumableContext(50);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            slot, flag);

        Assert.Equal(expectFixed ? 4000 : 20 * 50, delta);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    public void MpFixedOverride_ArmsForSlotsZeroThroughThree(int slot, bool expectFixed)
    {
        var consumable = new ConsumableContext(EatManaPotion: 40);

        var delta = StatCalculator.ManaElixirContributionWithOverride(consumable, EligibleZone,
            slot, 1);

        Assert.Equal(expectFixed ? 5000 : 25 * 40, delta);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 1, false)]
    [InlineData(0, 0, false)]
    public void DamageFixedOverride_InvertedGate_ArmsForSlotZeroOnly(int slot, int flag, bool expectFixed)
    {
        var consumable = new ConsumableContext(EatStrPotion: 50);

        var delta = StatCalculator.StrengthElixirAttackContributionWithOverride(consumable, EligibleZone,
            slot, flag);

        Assert.Equal(expectFixed ? 600 : 3 * 50, delta);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(-1, false)]
    public void AccuracyAndBlockFixedOverride_ArmForSlotsZeroThroughTwo(int slot, bool expectFixed)
    {
        var consumable = new ConsumableContext(EatDexPotion: 50);

        var accuracy = StatCalculator.AccuracyElixirContributionWithOverride(consumable, EligibleZone,
            slot, 1);
        var block = StatCalculator.BlockElixirContributionWithOverride(consumable, EligibleZone,
            slot, 1);

        Assert.Equal(expectFixed ? 400 : 2 * 50, accuracy);
        Assert.Equal(expectFixed ? 400 : 2 * 50, block);
    }

    [Fact]
    public void DexterityElixir_FeedsAccuracyAndBlockIndependently_TheDeliberateDoubleCount()
    {
        var consumable = new ConsumableContext(EatDexPotion: 100);

        var accuracyFixed = StatCalculator.AccuracyElixirContributionWithOverride(consumable, EligibleZone,
            0, 1);
        var blockFixed = StatCalculator.BlockElixirContributionWithOverride(consumable, EligibleZone,
            0, 1);
        Assert.Equal(400, accuracyFixed);
        Assert.Equal(400, blockFixed);

        var accuracyRaw = StatCalculator.AccuracyElixirContributionWithOverride(consumable, EligibleZone);
        var blockRaw = StatCalculator.BlockElixirContributionWithOverride(consumable, EligibleZone);
        Assert.Equal(2 * 100, accuracyRaw);
        Assert.Equal(2 * 100, blockRaw);
    }

    [Fact]
    public void FixedOverride_BeatsAMatchingEvent_LayerOneWins()
    {
        var consumable = new ConsumableContext(50, MaxPotionEventNum: 10);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            0, 1, 0, 0);

        Assert.Equal(4000, delta);
    }


    [Theory]
    [InlineData(10, 50, 2000)]
    [InlineData(10, 250, 20 * 250)]
    [InlineData(20, 150, 4000)]
    [InlineData(20, 250, 20 * 250)]
    [InlineData(30, 500, 6000)]
    [InlineData(99, 50, 20 * 50)]
    public void HpEventTier_MatchingTribe_AppliesCapsAndFlats(int tier, int count, int expected)
    {
        var consumable = new ConsumableContext(count, MaxPotionEventNum: tier);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: 0, eventTribe: 0);

        Assert.Equal(expected, delta);
    }

    [Theory]
    [InlineData(10, 50, 300)]
    [InlineData(20, 150, 600)]
    [InlineData(30, 500, 900)]
    [InlineData(10, 250, 3 * 250)]
    public void DamageEventTier_MatchingTribe_UsesDamageFlats(int tier, int count, int expected)
    {
        var consumable = new ConsumableContext(EatStrPotion: count, MaxPotionEventNum: tier);

        var delta = StatCalculator.StrengthElixirAttackContributionWithOverride(consumable, EligibleZone,
            avatarTribe: 2, eventTribe: 2);

        Assert.Equal(expected, delta);
    }

    [Fact]
    public void EventTier_AllTribesSentinel_MatchesEvenUnknownAvatarTribe()
    {
        var consumable = new ConsumableContext(EatManaPotion: 50, MaxPotionEventNum: 10);

        var delta = StatCalculator.ManaElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: -1, eventTribe: 4);

        Assert.Equal(2500, delta);
    }

    [Fact]
    public void EventDisabledByDefault_FallsToRawFloor_AndDoesNotFalseMatchUnknownTribe()
    {
        var consumable = new ConsumableContext(50, MaxPotionEventNum: 10);

        var disabledDefault = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone);
        var unknownTribeTrap = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: -1, eventTribe: -1);

        Assert.Equal(20 * 50, disabledDefault);
        Assert.Equal(20 * 50, unknownTribeTrap);
    }

    [Fact]
    public void EventTribeMismatch_FallsToRawFloor()
    {
        var consumable = new ConsumableContext(50, MaxPotionEventNum: 10);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: 0, eventTribe: 1);

        Assert.Equal(20 * 50, delta);
    }


    [Fact]
    public void ResolveFourGuildSlot_MatchingName_ReturnsMatchIndex()
    {
        string[] names = ["Alpha", "Beta", "Gamma", "Delta"];

        Assert.Equal(0, StatCalculator.ResolveFourGuildSlot(0, "Alpha", names));
        Assert.Equal(2, StatCalculator.ResolveFourGuildSlot(0, "Gamma", names));
        Assert.Equal(3, StatCalculator.ResolveFourGuildSlot(3, "Delta", names));
    }

    [Fact]
    public void ResolveFourGuildSlot_NoMatchOrOutOfRangeTribe_ReturnsNone()
    {
        string[] names = ["Alpha", "Beta", "Gamma", "Delta"];

        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(0, "Zeta", names));
        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(4, "Alpha", names));
        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(-1, "Alpha", names));
        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(0, "", names));
    }

    [Fact]
    public void ResolveFourGuildSlot_FewerThanFourDesignatedNames_MatchesWithinBounds()
    {
        string[] partial = ["First", "Second"];

        Assert.Equal(1, StatCalculator.ResolveFourGuildSlot(1, "Second", partial));
        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(1, "Third", partial));
    }

    [Fact]
    public void ResolvedSlot_ThenFixedOverride_EndToEnd()
    {
        string[] tribe0Names = ["Royal", "Imperial", "Sovereign", "Regent"];
        var slot = StatCalculator.ResolveFourGuildSlot(0, "Royal", tribe0Names);
        Assert.Equal(0, slot);

        var consumable = new ConsumableContext(5);
        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            slot, 1);

        Assert.Equal(4000, delta);
    }
}
