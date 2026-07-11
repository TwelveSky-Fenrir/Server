using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Port of <c>PETSYSTEM</c>'s once-per-120-ticks double-pet-EXP-timer freeze gate
///     (<c>S07_MyGame04.cpp:942-953</c>): the gate that decides whether
///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.PetExpX2Time" /> keeps draining or
///     freezes, based on the equipped pet's growth percentage (<c>PETSYSTEM::ReturnGrowPercent</c>,
///     <c>GameSystem_07_Pet.cpp:437-530</c>).
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : <c>GameSystem_07_Pet.cpp:437-530</c> (the live growth-percent formula --
///         <see cref="StatCalculator.PetGrowPercent" /> already ports this exactly);
///         <c>S07_MyGame04.cpp:936-940</c> (a commented-out, unconditional-decrement predecessor confirming
///         the gated version immediately below it is the deliberate, current implementation);
///         <c>S07_MyGame04.cpp:942-953</c> (the live gate itself: equip-slot lookup, pet-sort-category check,
///         below-200%-comparison, conditional decrement, client notification).
///     </para>
///     <para>
///         <b>Item-id -&gt; category table (Table A per the B8-pet-growth-depth contract).</b> This is
///         deliberately its OWN table, not a reuse of <see cref="PetGrowthTierCalculator" />'s (built for the
///         unrelated <c>ReturnGrowStep</c> tier-crossing broadcast) or
///         <see cref="PetSteppedAttackPowerCategoryTable" />'s (the six-tier attack-power bonus, a strictly
///         smaller subset) -- the contract's own re-verification of both switch statements found their
///         non-GIFT_EVENT id memberships genuinely differ; do not collapse these into one shared table.
///     </para>
///     <para>
///         Includes the GIFT_EVENT-gated 8200-series ids. This is a correction relative to the OLDER
///         <see cref="PetGrowthTierCalculator" />/<see cref="PetExperienceCreditCalculator" />/
///         <see cref="PetGrowthCalculator" /> tables (each omits the 8200-series under a since-superseded
///         belief that the gating macro is never compiled): the B8-pet-growth-depth contract re-verified,
///         this session, that the macro chain (<c>Protocol/DEFINE.h:21-23,103,110-117</c>,
///         <c>ts25latest_config.props:4-11</c>) IS live under both real build configurations. Reconciling the
///         three older tables with this finding is a separate follow-up, not this gate's own concern -- see
///         this workstream's agent-memory notes.
///     </para>
///     <para>
///         The pet-sort-category check is deliberately folded into table membership rather than modeled as a
///         separate <c>WorldDataCache.ItemsById</c> lookup: every id below is a curated, dedicated
///         pet-growth item (iSort 22 by construction in the legacy item catalog), so a Table-A hit already
///         implies the sort check would pass -- adding a redundant <c>ItemDefinition</c> dependency here would
///         only cost a new DI wire with no behavioral difference for any id this table actually lists. Any
///         item id NOT in this table (including a real item of some other sort) is treated identically to "no
///         pet equipped" per the contract's own edge case: the gate is never reached, so the timer keeps
///         draining as if the gate had never fired.
///     </para>
///     <para>
///         The 8 hardcoded server numbers and 6 RvR/tower-war zone-type flags that skip the ENTIRE
///         once-per-120-ticks block this gate lives inside (<c>S07_MyGame04.cpp:913-929</c>) are NOT modeled
///         here, matching <c>PlayTimeAccrualSystem</c>'s own remarks flagging that same
///         exclusion block as out of its asserted scope -- the source contract itself does not enumerate the
///         concrete server/zone-type values, only the citation range, so inventing them here would violate
///         the "never invent an id" rule. See openQuestions.
///     </para>
/// </remarks>
public static class DoublePetExpTimerGate
{
    private static readonly FrozenDictionary<int, int> CategoryByItemId = new Dictionary<int, int>
    {
        // Category 0: legacy ids + GIFT_EVENT 8202-8205.
        [541] = 0, [542] = 0, [547] = 0, [560] = 0, [1002] = 0, [1003] = 0, [1004] = 0, [1005] = 0, [2140] = 0,
        [8202] = 0, [8203] = 0, [8204] = 0, [8205] = 0,

        // Category 1: legacy ids + GIFT_EVENT 8206-8211.
        [543] = 1, [544] = 1, [548] = 1, [561] = 1, [1006] = 1, [1007] = 1, [1008] = 1, [1009] = 1, [1010] = 1,
        [1011] = 1, [1452] = 1, [17052] = 1, [86819] = 1,
        [8206] = 1, [8207] = 1, [8208] = 1, [8209] = 1, [8210] = 1, [8211] = 1,

        // Category 2: legacy ids + GIFT_EVENT 8212-8215.
        [545] = 2, [549] = 2, [562] = 2, [1012] = 2, [1013] = 2, [1014] = 2, [1015] = 2, [17053] = 2, [86820] = 2,
        [8212] = 2, [8213] = 2, [8214] = 2, [8215] = 2,

        // Category 3: legacy ids + GIFT_EVENT 8216.
        [546] = 3, [550] = 3, [1016] = 3, [1310] = 3, [1311] = 3, [1312] = 3, [2133] = 3, [2144] = 3, [2160] = 3,
        [17055] = 3, [17056] = 3, [17057] = 3,
        [8216] = 3
    }.ToFrozenDictionary();

    /// <summary>
    ///     The growth percentage the gate uses to decide freeze-or-drain, or 0 for an item id absent from
    ///     Table A -- matching the contract's edge case that an unrecognized pet "is, from the gate's point
    ///     of view, always below 200 percent."
    /// </summary>
    public static float ResolveGrowthPercent(int petItemId, int growth)
    {
        return CategoryByItemId.TryGetValue(petItemId, out var categoryIndex)
            ? StatCalculator.PetGrowPercent(growth, PetGrowthCaps.Values[categoryIndex])
            : 0f;
    }

    /// <summary>Whether the equipped pet has reached the 200% ceiling that freezes the timer's countdown.</summary>
    public static bool IsAtFreezeThreshold(int petItemId, int growth)
    {
        return ResolveGrowthPercent(petItemId, growth) >= 200f;
    }

    /// <summary>
    ///     The double-pet-EXP timer's value after one once-per-120-ticks gate evaluation: unchanged if
    ///     <paramref name="currentTimerValue" /> is already zero/negative (mirrors the legacy's own enclosing
    ///     "timer &gt; 0" guard -- a harmless no-op, not an error), unchanged if the growth percentage has
    ///     reached the 200% ceiling, otherwise drained by exactly one.
    /// </summary>
    public static int ComputeNextTimerValue(int petItemId, int growth, int currentTimerValue)
    {
        if (currentTimerValue <= 0)
            return currentTimerValue;

        return IsAtFreezeThreshold(petItemId, growth) ? currentTimerValue : currentTimerValue - 1;
    }
}
