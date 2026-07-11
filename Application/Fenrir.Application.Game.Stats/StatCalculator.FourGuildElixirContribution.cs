using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    /// <summary>No four-guild slot resolved (tribe out of range, or guild name not one of the four designated).</summary>
    public const int FourGuildSlotNone = -1;

    /// <summary>Default/disabled active-event tribe -- matches no tribe and no sentinel (S07_MyGame04.cpp:119).</summary>
    private const int EventDisabledTribe = -1;

    /// <summary>All-tribes event sentinel -- an event set to this value targets every tribe (DEFINE.h:309, MAX_TRIBE_NUM).</summary>
    private const int AllTribesEventTribeSentinel = 4;

    /// <summary>
    ///     Tier-10 potion-event count cap: the flat tier grant applies only while the count is below this
    ///     (MyFactor.cpp:581-596).
    /// </summary>
    private const int EventTier10CountCap = 100;

    /// <summary>Tier-20 potion-event count cap (MyFactor.cpp:589-596 and the parallel MP/DMG/hit/block branches).</summary>
    private const int EventTier20CountCap = 200;

    /// <summary>Accuracy/block raw per-elixir rate (MyFactor.cpp:694,699 / 726,731). B2 used this inline; B7 names it.</summary>
    private const int DexterityElixirRate = 2;

    /// <summary>
    ///     The per-family value table (MyFactor.cpp:574-734). Built once at static init; read lock-free on every
    ///     base-stat recompute. <see cref="FourGuildElixirParams.FixedOverrideSlots" /> holds each family's
    ///     armed-slot set as a <see cref="FrozenSet{Int32}" /> for O(1), allocation-free membership on the read path.
    /// </summary>
    private static readonly FrozenDictionary<FourGuildElixirStat, FourGuildElixirParams> FourGuildElixirTable =
        BuildFourGuildElixirTable();

    private static FrozenDictionary<FourGuildElixirStat, FourGuildElixirParams> BuildFourGuildElixirTable()
    {
        // Built as locals so there is no static-field initialization-order dependency; accuracy and block share
        // one set (identical logic and values -- MyFactor.cpp:704-734).
        var hpSlots = new[] { 0, 1 }.ToFrozenSet(); // MyFactor.cpp:576
        var mpSlots = new[] { 0, 1, 2, 3 }.ToFrozenSet(); // MyFactor.cpp:607
        var damageSlots = new[] { 0 }.ToFrozenSet(); // MyFactor.cpp:639 (inverted gate -> slot 0 only)
        var accuracyBlockSlots = new[] { 0, 1, 2 }.ToFrozenSet(); // MyFactor.cpp:674 / :706

        return new Dictionary<FourGuildElixirStat, FourGuildElixirParams>
        {
            // family                     fixed  slots               t10   t20   t30   rawRate
            [FourGuildElixirStat.Life] = new(4000, hpSlots, 2000, 4000, 6000, LifeElixirRate), // :574-603
            [FourGuildElixirStat.Mana] = new(5000, mpSlots, 2500, 5000, 7500, ManaElixirRate), // :605-635
            [FourGuildElixirStat.Damage] = new(600, damageSlots, 300, 600, 900, StrengthElixirAttackRate), // :637-670
            [FourGuildElixirStat.Accuracy] =
                new(400, accuracyBlockSlots, 200, 400, 600, DexterityElixirRate), // :672-702
            [FourGuildElixirStat.Block] = new(400, accuracyBlockSlots, 200, 400, 600, DexterityElixirRate) // :704-734
        }.ToFrozenDictionary();
    }

    /// <summary>
    ///     The full three-layer non-elemental elixir computation for one stat family (MyFactor.cpp:574-734), NOT
    ///     zone-gated (the caller applies the zone gate). Returns the additive whole-number delta the family's base
    ///     getter folds in. Faithful to the exact legacy branch order: B4G fixed override, then matching-event tier
    ///     (with the tier-10/20 count caps and tier-30 no-cap), then the raw per-potion floor.
    /// </summary>
    /// <param name="stat">Which non-elemental family is being augmented.</param>
    /// <param name="potionCount">The family's own persisted potion counter (life/mana/strength/dexterity).</param>
    /// <param name="b4gFlag">The family's B4G opt-in flag; the fixed override arms only when this equals exactly 1.</param>
    /// <param name="fourGuildSlot">Resolved four-guild slot 0..3, or <see cref="FourGuildSlotNone" /> (-1).</param>
    /// <param name="avatarTribe">The character's current playable tribe, for the per-tribe event match; -1 = unknown.</param>
    /// <param name="eventTribe">Active-event tribe: 0..3 one tribe, 4 all tribes, -1 disabled.</param>
    /// <param name="eventTier">Active-event tier: only 10, 20, 30 carry meaning; anything else falls to the raw floor.</param>
    private static int ComputeFourGuildElixirDelta(
        FourGuildElixirStat stat,
        int potionCount,
        int b4gFlag,
        int fourGuildSlot,
        int avatarTribe,
        int eventTribe,
        int eventTier)
    {
        var p = FourGuildElixirTable[stat];

        // Layer 1 -- fixed B4G override. Fires only when the opt-in flag is exactly one AND the resolved slot is
        // one this family arms. A slot of -1 (none) is never in any family's set, so it can never fire.
        if (b4gFlag == 1 && p.FixedOverrideSlots.Contains(fourGuildSlot))
            return p.FixedOverride;

        // Layer 2 -- matching potion-event tier. The event matches only when it is enabled (a real tribe or the
        // all-tribes sentinel), never on the disabled -1 default -- the >= 0 guard also stops a -1==-1 false match
        // when the avatar tribe is likewise unknown.
        var eventMatches = eventTribe >= 0 &&
                           (eventTribe == avatarTribe || eventTribe == AllTribesEventTribeSentinel);
        if (eventMatches)
            return eventTier switch
            {
                10 => potionCount < EventTier10CountCap ? p.EventTier10Bonus : p.RawRatePerPotion * potionCount,
                20 => potionCount < EventTier20CountCap ? p.EventTier20Bonus : p.RawRatePerPotion * potionCount,
                30 => p.EventTier30Bonus,
                _ => p.RawRatePerPotion * potionCount
            };

        // Layer 3 -- raw per-potion floor (identical to the B2 *ElixirContribution methods).
        return p.RawRatePerPotion * potionCount;
    }

    /// <summary>
    ///     Life-elixir -> max HP with the B4G/event override layers (MyFactor.cpp:574-603). Zone-gated by
    ///     <see cref="IsElixirEligibleZone" />. With every override input at its default (<see cref="FourGuildSlotNone" />,
    ///     flag 0, tribes -1) this is byte-identical to the B2 <c>LifeElixirContribution</c> raw floor -- the
    ///     property that makes it a safe drop-in at the <c>ComputeMaxLife</c> call site before its inputs land.
    /// </summary>
    public static int LifeElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int hpElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Life, consumable.EatLifePotion,
            hpElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

    /// <summary>
    ///     Mana-elixir -> max MP with the B4G/event override layers (MyFactor.cpp:605-635). See
    ///     <see cref="LifeElixirContributionWithOverride" /> for the drop-in/degradation contract.
    /// </summary>
    public static int ManaElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int mpElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Mana, consumable.EatManaPotion,
            mpElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

    /// <summary>
    ///     Strength-elixir -> attack power with the B4G/event override layers (MyFactor.cpp:637-670). Note the
    ///     "inverted gate": the fixed +600 arms only for four-guild slot exactly 0 (with the strength flag set) --
    ///     a non-slot-0 member with the strength elixir active gets the normal event/raw path, not the fixed value.
    /// </summary>
    public static int StrengthElixirAttackContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int strElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Damage, consumable.EatStrPotion,
            strElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

    /// <summary>
    ///     Dexterity-elixir -> accuracy/hit with the B4G/event override layers (MyFactor.cpp:672-702, the
    ///     naming-trap <c>ELR_DEF</c> event that feeds hit). Reads the SAME dexterity counter and dexterity B4G
    ///     flag as <see cref="BlockElixirContributionWithOverride" /> -- the deliberate double-count (MyFactor.cpp:3149,3321).
    /// </summary>
    public static int AccuracyElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int dexElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Accuracy, consumable.EatDexPotion,
            dexElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

    /// <summary>
    ///     Dexterity-elixir -> block/dodge with the B4G/event override layers (MyFactor.cpp:704-734, the
    ///     <c>ELR_DODGE</c> event). Byte-for-byte identical logic and values to
    ///     <see cref="AccuracyElixirContributionWithOverride" />, reading the same dexterity inputs.
    /// </summary>
    public static int BlockElixirContributionWithOverride(
        ConsumableContext consumable,
        ZoneContext zone,
        int fourGuildSlot = FourGuildSlotNone,
        int dexElixirB4GFlag = 0,
        int avatarTribe = EventDisabledTribe,
        int eventTribe = EventDisabledTribe)
    {
        if (!IsElixirEligibleZone(zone.ZoneNumber))
            return 0;
        return ComputeFourGuildElixirDelta(FourGuildElixirStat.Block, consumable.EatDexPotion,
            dexElixirB4GFlag, fourGuildSlot, avatarTribe, eventTribe, consumable.MaxPotionEventNum);
    }

    /// <summary>
    ///     Resolves the four-guild ("B4G") slot for a character (CheckFourGuildName, MyFactor.cpp:105-126): when
    ///     the tribe is 0..3 and the guild name equals one of that tribe's four designated guild names, the slot is
    ///     that match index (0..3); otherwise <see cref="FourGuildSlotNone" /> (-1). A tribe outside 0..3 always
    ///     yields -1 and no override is possible.
    /// </summary>
    /// <remarks>
    ///     This captures only the matching RULE. The reference DATA -- the per-tribe designated guild names
    ///     (mFourGuildName, STRUCT.h:634) -- is world-config that no B1 context carries, so the four names for the
    ///     tribe must be supplied by the caller (the wiring/assembly pass), never invented here. The comparison is
    ///     ordinal; the exact fixed-char-field null-termination/case semantics of the legacy compare are deferred
    ///     with the rest of the guild-name threading (see openQuestions).
    /// </remarks>
    /// <param name="avatarTribe">The character's current playable tribe.</param>
    /// <param name="guildName">The character's guild name.</param>
    /// <param name="tribeFourGuildNames">The four designated guild names for <paramref name="avatarTribe" />.</param>
    public static int ResolveFourGuildSlot(
        int avatarTribe,
        ReadOnlySpan<char> guildName,
        ReadOnlySpan<string> tribeFourGuildNames)
    {
        if (avatarTribe is < 0 or >= AllTribesEventTribeSentinel)
            return FourGuildSlotNone;
        if (guildName.IsEmpty)
            return FourGuildSlotNone;

        var count = Math.Min(tribeFourGuildNames.Length, AllTribesEventTribeSentinel);
        for (var slot = 0; slot < count; slot++)
        {
            var designated = tribeFourGuildNames[slot];
            if (!string.IsNullOrEmpty(designated) && guildName.SequenceEqual(designated))
                return slot;
        }

        return FourGuildSlotNone;
    }
    // ---- Four-guild ("B4G") elixir override + potion-event tier layer (WORKSTREAM B7) ----
    //
    // MyFactor::GetZoneForElixir (Server/Header/Protocol/MyFactor.cpp:549-739) is a three-layer decision for each
    // of the five NON-elemental stat families (HP, MP, damage, accuracy, block). WORKSTREAM B2 (see
    // StatCalculator.Life.cs) implemented the bottom layer -- the raw per-elixir multiplier -- and left the two
    // upper override layers deferred because their inputs had no channel into the calculator. B7 adds those two
    // layers as a faithful, self-contained engine that DEGRADES to the exact B2 raw floor when its extra inputs
    // are absent (four-guild slot = -1, all B4G flags = 0, event disabled), so it can be dropped in at each
    // getter call site behavior-identically today and light up as the missing context fields land.
    //
    // The three layers, resolved in this order for every non-elemental family (MyFactor.cpp:574-734):
    //   1. Fixed four-guild ("B4G") override -- a flat value that REPLACES the whole computation, fired only when
    //      the per-family B4G opt-in flag equals exactly one AND the character's resolved four-guild slot is one
    //      this family honors (HP {0,1}, MP {0,1,2,3}, damage {0}, accuracy/block {0,1,2}).
    //   2. Potion-event tier -- when a live event targets the avatar's tribe (or the all-tribes sentinel), tiers
    //      10/20/30 grant a flat family-specific bonus; tiers 10 and 20 only while the family's potion count is
    //      still below the cap (100 / 200), tier 30 with no cap. Any other tier falls through to layer 3.
    //   3. Raw multiplier -- the per-elixir floor B2 already models (rate x potion count), also the fallback for a
    //      disabled/non-matching event and a saturated tier-10/20 cap.
    //
    // The whole non-elemental path is gated by the SAME zone-eligibility test B2 uses (IsElixirEligibleZone,
    // StatCalculator.Life.cs); an ineligible zone yields zero and the four-guild slot is never even resolved
    // (MyFactor.cpp:554,568). The two ELEMENTAL families are deliberately NOT part of B7: their branch runs before
    // the four-guild slot is resolved and can never receive an override (MyFactor.cpp:555-566), so they keep the
    // flat B2 packed-field decode untouched.
    //
    // MISSING INPUTS (see the workstream openQuestions -- these stay at inert defaults, never invented):
    //   * The character's resolved four-guild slot -- needs the current playable tribe, the character's guild
    //     NAME, and the world four-guild-name table (per tribe, four designated names). None are carried by any
    //     B1 context record. Taken here as an explicit fourGuildSlot parameter (default -1 = none); the matching
    //     RULE is captured by ResolveFourGuildSlot below, but the world name table is not modeled.
    //   * The four B4G opt-in flags (HP/MP/strength/dexterity, STRUCT.h:483-486) -- not on ConsumableContext.
    //     Taken as explicit flag parameters (default 0 = off).
    //   * The active-event tribe -- ConsumableContext.EventTribe is a byte whose default (0) is a VALID tribe and
    //     which cannot represent the legacy "disabled" sentinel (-1), so reading it directly would false-match an
    //     event for tribe 0 (and specifically break the B1 non-leakage guard, whose populated fixture sets
    //     EventTribe:1 + MaxPotionEventNum:20 with zero counts). Taken as an explicit eventTribe parameter
    //     (default -1 = disabled) until that field becomes signed/nullable. The event TIER, by contrast, has a
    //     valid channel (ConsumableContext.MaxPotionEventNum) and IS read from the context.
    //
    //   WORKSTREAM B7-fourguild-eventtier-source (wave14) RESOLVED this seam's open question -- not by finding a
    //   source, but by proving there is none to find: a full-tree read of all 9 executables plus Header/Lib
    //   found ZERO write sites anywhere for the four aB4G*Elixir flags, ZERO population sites for the world
    //   mFourGuildName table, and confirmed mMaxPotionEventTribe/mMaxPotionEventNum are plain per-session
    //   MyFactor-instance members (not AVATAR_INFO/persisted-avatar fields at all) whose only setter is a
    //   self-referential no-op (Server/ts25zone/S07_MyGame04.cpp:158-170) -- every one of these six inputs is
    //   therefore provably always at its constructor-default/reset value (0, -1, or absent) in the compiled
    //   legacy build. There is consequently no legacy DATA these parameters could ever be threaded from: adding
    //   a PlayerRuntimeState field for e.g. the B4G flags would be inventing a brand-new Fenrir-only activation
    //   mechanism (a GM command, an admin tool, a new event feature) for a system legacy itself never turns on
    //   -- explicitly a product decision per that contract's own "Open questions summary", not a citation gap
    //   this pass can close. The engine above is therefore intentionally left exactly as-is: correct, fully
    //   tested (FourGuildElixirContributionTests.cs), and permanently degraded to the raw floor in production
    //   until such a product decision is made.

    /// <summary>The five non-elemental stat families that receive the B4G/event override (MyFactor.cpp:574-734).</summary>
    /// <remarks>
    ///     Naming trap (MyFactor.cpp:3149): the legacy <c>ELR_DEF</c> event feeds ACCURACY/hit, not defense --
    ///     modelled here as <see cref="Accuracy" />. Defense power has no elixir call site at all.
    /// </remarks>
    private enum FourGuildElixirStat : byte
    {
        Life = 0,
        Mana = 1,
        Damage = 2,
        Accuracy = 3,
        Block = 4
    }

    /// <summary>
    ///     One non-elemental family's cited magnitudes: the fixed B4G override, the slots that arm it, the three
    ///     event-tier flat bonuses, and the raw per-potion rate that both the B2 floor and the tier fall-through use.
    /// </summary>
    private readonly record struct FourGuildElixirParams(
        int FixedOverride,
        FrozenSet<int> FixedOverrideSlots,
        int EventTier10Bonus,
        int EventTier20Bonus,
        int EventTier30Bonus,
        int RawRatePerPotion);
}
