using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B7 -- the four-guild ("B4G") fixed override and the potion-event tier layers that sit on top of
///     the B2 raw per-elixir floor inside <c>MyFactor::GetZoneForElixir</c>
///     (Server/Header/Protocol/MyFactor.cpp:574-734). These exercise the public
///     <c>*ElixirContributionWithOverride</c> engine directly with explicit override inputs, because those inputs
///     (four-guild slot, the four B4G flags, the active-event tribe) have no channel through the B1 stat-context
///     records yet and so cannot be driven via <c>ComputeBaseStats</c> the way the B2 raw floor is
///     (<see cref="ConsumableElixirStatFeedTests" />). Every expectation is int-truncation-exact.
///     <para>
///         Degradation invariant: with every override input at its default (slot -1, flag 0, tribes -1) each
///         method reproduces the exact B2 raw floor, which is what lets a getter call site swap the B2 term for
///         the WithOverride term behavior-identically today.
///     </para>
/// </summary>
public class FourGuildElixirContributionTests
{
    private static readonly ZoneContext EligibleZone = new(ZoneNumber: 1);

    // ---- Layer 3: raw floor parity with B2 (default override inputs) ----

    [Fact]
    public void AllFamilies_DefaultOverrides_ReproduceB2RawFloor()
    {
        var life = new ConsumableContext(EatLifePotion: 50);
        var mana = new ConsumableContext(EatManaPotion: 50);
        var str = new ConsumableContext(EatStrPotion: 50);
        var dex = new ConsumableContext(EatDexPotion: 50);

        Assert.Equal(20 * 50, StatCalculator.LifeElixirContributionWithOverride(life, EligibleZone)); // 1000
        Assert.Equal(25 * 50, StatCalculator.ManaElixirContributionWithOverride(mana, EligibleZone)); // 1250
        Assert.Equal(3 * 50, StatCalculator.StrengthElixirAttackContributionWithOverride(str, EligibleZone)); // 150
        Assert.Equal(2 * 50, StatCalculator.AccuracyElixirContributionWithOverride(dex, EligibleZone)); // 100
        Assert.Equal(2 * 50, StatCalculator.BlockElixirContributionWithOverride(dex, EligibleZone)); // 100
    }

    [Fact]
    public void LifeRawFloor_HoldsInReEnableBandAndNormalZone()
    {
        // 319..323 is the concrete elixir re-enable band; a plain zone is eligible by default too (suppression is
        // an opaque, still-deferred classifier -- there is no currently-ineligible zone to assert a 0 against).
        var consumable = new ConsumableContext(EatLifePotion: 10);

        foreach (short zoneNumber in (short[])[1, 319, 320, 323])
        {
            var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, new ZoneContext(zoneNumber));
            Assert.Equal(20 * 10, delta); // 200
        }
    }

    // ---- Layer 1: fixed four-guild ("B4G") override ----

    [Theory]
    [InlineData(0, 1, true)] // slot 0 armed for HP
    [InlineData(1, 1, true)] // slot 1 armed for HP
    [InlineData(2, 1, false)] // slot 2 NOT in {0,1} -> raw
    [InlineData(0, 0, false)] // flag off -> raw
    [InlineData(-1, 1, false)] // no slot resolved -> raw
    public void HpFixedOverride_ArmsForSlotsZeroAndOne_WithFlagSet(int slot, int flag, bool expectFixed)
    {
        var consumable = new ConsumableContext(EatLifePotion: 50);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: slot, hpElixirB4GFlag: flag);

        Assert.Equal(expectFixed ? 4000 : 20 * 50, delta);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)] // MP arms the widest slot set {0,1,2,3}
    [InlineData(-1, false)]
    public void MpFixedOverride_ArmsForSlotsZeroThroughThree(int slot, bool expectFixed)
    {
        var consumable = new ConsumableContext(EatManaPotion: 40);

        var delta = StatCalculator.ManaElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: slot, mpElixirB4GFlag: 1);

        Assert.Equal(expectFixed ? 5000 : 25 * 40, delta);
    }

    [Theory]
    [InlineData(0, 1, true)] // the ONLY case that arms damage: slot 0 + strength flag
    [InlineData(1, 1, false)] // inverted gate: slot 1 with strength elixir active still gets NO fixed +600
    [InlineData(2, 1, false)]
    [InlineData(0, 0, false)] // slot 0 without the strength flag: no fixed value either
    public void DamageFixedOverride_InvertedGate_ArmsForSlotZeroOnly(int slot, int flag, bool expectFixed)
    {
        var consumable = new ConsumableContext(EatStrPotion: 50);

        var delta = StatCalculator.StrengthElixirAttackContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: slot, strElixirB4GFlag: flag);

        Assert.Equal(expectFixed ? 600 : 3 * 50, delta);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)] // accuracy/block arm {0,1,2}
    [InlineData(3, false)] // slot 3 not armed -> raw
    [InlineData(-1, false)]
    public void AccuracyAndBlockFixedOverride_ArmForSlotsZeroThroughTwo(int slot, bool expectFixed)
    {
        var consumable = new ConsumableContext(EatDexPotion: 50);

        var accuracy = StatCalculator.AccuracyElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: slot, dexElixirB4GFlag: 1);
        var block = StatCalculator.BlockElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: slot, dexElixirB4GFlag: 1);

        Assert.Equal(expectFixed ? 400 : 2 * 50, accuracy);
        Assert.Equal(expectFixed ? 400 : 2 * 50, block);
    }

    [Fact]
    public void DexterityElixir_FeedsAccuracyAndBlockIndependently_TheDeliberateDoubleCount()
    {
        // One dexterity B4G elixir active for a slot-0 member grants +400 to accuracy AND +400 to block
        // (MyFactor.cpp:3149,3321) -- and the raw floor likewise counts the same dexterity counter into both.
        var consumable = new ConsumableContext(EatDexPotion: 100);

        var accuracyFixed = StatCalculator.AccuracyElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: 0, dexElixirB4GFlag: 1);
        var blockFixed = StatCalculator.BlockElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: 0, dexElixirB4GFlag: 1);
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
        // slot 0 + HP flag arms the fixed +4000 even when a matching tier-10 event is also active (which would
        // otherwise grant +2000). Layer 1 short-circuits before the event path.
        var consumable = new ConsumableContext(EatLifePotion: 50, MaxPotionEventNum: 10);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: 0, hpElixirB4GFlag: 1, avatarTribe: 0, eventTribe: 0);

        Assert.Equal(4000, delta); // fixed, not the tier's 2000
    }

    // ---- Layer 2: matching potion-event tiers ----

    [Theory]
    [InlineData(10, 50, 2000)] // tier 10, count below 100 -> flat 2000
    [InlineData(10, 250, 20 * 250)] // tier 10, count at/over cap -> falls through to raw
    [InlineData(20, 150, 4000)] // tier 20, count below 200 -> flat 4000
    [InlineData(20, 250, 20 * 250)] // tier 20, count over cap -> raw
    [InlineData(30, 500, 6000)] // tier 30, no cap -> always flat 6000
    [InlineData(99, 50, 20 * 50)] // unrecognised tier -> raw
    public void HpEventTier_MatchingTribe_AppliesCapsAndFlats(int tier, int count, int expected)
    {
        var consumable = new ConsumableContext(EatLifePotion: count, MaxPotionEventNum: tier);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: 0, eventTribe: 0);

        Assert.Equal(expected, delta);
    }

    [Theory]
    [InlineData(10, 50, 300)] // damage tier flats are 300 / 600 / 900
    [InlineData(20, 150, 600)]
    [InlineData(30, 500, 900)]
    [InlineData(10, 250, 3 * 250)] // over cap -> raw (rate 3)
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
        // eventTribe 4 = all tribes: applies regardless of the avatar's tribe (here left unknown at -1).
        var consumable = new ConsumableContext(EatManaPotion: 50, MaxPotionEventNum: 10);

        var delta = StatCalculator.ManaElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: -1, eventTribe: 4);

        Assert.Equal(2500, delta); // MP tier-10 flat
    }

    [Fact]
    public void EventDisabledByDefault_FallsToRawFloor_AndDoesNotFalseMatchUnknownTribe()
    {
        // Default eventTribe -1 must never match -- including the -1 == -1 trap when the avatar tribe is also
        // unknown. Even with a live tier configured, a disabled event yields the raw floor.
        var consumable = new ConsumableContext(EatLifePotion: 50, MaxPotionEventNum: 10);

        var disabledDefault = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone);
        var unknownTribeTrap = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: -1, eventTribe: -1);

        Assert.Equal(20 * 50, disabledDefault); // 1000, raw
        Assert.Equal(20 * 50, unknownTribeTrap); // 1000, raw -- NOT the tier's 2000
    }

    [Fact]
    public void EventTribeMismatch_FallsToRawFloor()
    {
        // A live event targeting tribe 1 does not apply to a tribe-0 avatar.
        var consumable = new ConsumableContext(EatLifePotion: 50, MaxPotionEventNum: 10);

        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            avatarTribe: 0, eventTribe: 1);

        Assert.Equal(20 * 50, delta); // 1000, raw
    }

    // ---- Four-guild slot resolver (CheckFourGuildName, MyFactor.cpp:105-126) ----

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
        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(4, "Alpha", names)); // tribe out of 0..3
        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(-1, "Alpha", names));
        Assert.Equal(StatCalculator.FourGuildSlotNone, StatCalculator.ResolveFourGuildSlot(0, "", names)); // empty guild name
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
        // Resolve a slot from tribe + guild name, then feed it to the contribution engine: an official-guild
        // (slot 0) member with the HP elixir opt-in gets the fixed +4000 regardless of counters.
        string[] tribe0Names = ["Royal", "Imperial", "Sovereign", "Regent"];
        var slot = StatCalculator.ResolveFourGuildSlot(0, "Royal", tribe0Names);
        Assert.Equal(0, slot);

        var consumable = new ConsumableContext(EatLifePotion: 5);
        var delta = StatCalculator.LifeElixirContributionWithOverride(consumable, EligibleZone,
            fourGuildSlot: slot, hpElixirB4GFlag: 1);

        Assert.Equal(4000, delta);
    }
}
