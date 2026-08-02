using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     Pure formula class for the four instant-EXP pill items: 649, 650, 1489, and 1490.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:4112-4220 — LNW33-gated block.
///     All four items are live in every build configuration; LNW33 is permanently active in the
///     repository's one real buildable configuration (ReleaseEU33).
///
///     Per-item rules:
///     <list type="bullet">
///         <item>
///             <term>649</term>
///             <description>
///                 No level gate. Gain = ⌈(factor2 − factor1) / 100⌉ × 5, where factor1 =
///                 <c>ExpRangeMin</c> and factor2 = <c>ExpRangeMax</c> for the character's current
///                 level (or 2,000,000,000 at level 145 — see the max-level note below).
///             </description>
///         </item>
///         <item>
///             <term>650</term>
///             <description>Same band formula as 649, multiplier × 10.</description>
///         </item>
///         <item>
///             <term>1489</term>
///             <description>
///                 Gate: level1 ≥ 113 AND level2 == 0. Same band formula, multiplier × 3.
///             </description>
///         </item>
///         <item>
///             <term>1490</term>
///             <description>
///                 Gate: level2 ≥ 1. Gain = ⌈ReturnHighExpValue(level2) / 100⌉ × 1, where
///                 ReturnHighExpValue maps to <see cref="RebirthProgression.HighLevelExpTable" />[level2 − 1].
///             </description>
///         </item>
///     </list>
///
///     Max-level note (items 649/650/1489): at level 145, factor2 is MAX_NUMBER_SIZE (2,000,000,000)
///     rather than the table's own <c>ExpRangeMax</c>, matching the legacy special-case in
///     Server/ts25zone/S04_MyWork03.cpp:4143-4148.
///
///     Absolute-cap check (all four items): if level1 == 145 and exp1 == 2,000,000,000 the item fails
///     before any gain is computed — this is the only case where there is literally no EXP pool
///     left to fill (Server/ts25zone/S04_MyWork03.cpp:4150-4155).
///
///     ExpFlag gate: legacy checks <c>wAuth.ExpFlag</c> and rejects the pill when it is set (a
///     server-side anti-cheat EXP lock). Fenrir has no equivalent flag — the gate is omitted.
/// </remarks>
public static class InstantExpPillFormulas
{
    /// <summary>Returns true when <paramref name="itemId" /> is one of the four instant-EXP pill items.</summary>
    public static bool IsInstantExpPill(int itemId) => itemId is 649 or 650 or 1489 or 1490;

    /// <summary>
    ///     Returns true when the character is at the absolute EXP ceiling: level 145 with main experience
    ///     already at or above <see cref="HighLevelExperienceResolver.MaxMainExperience" /> (2,000,000,000).
    ///     Any EXP pill must be rejected without consumption in this state.
    /// </summary>
    public static bool IsAtAbsoluteExpCeiling(short level, long experience)
        => level == LevelProgressionCalculator.MaxLevel
           && experience >= HighLevelExperienceResolver.MaxMainExperience;

    /// <summary>
    ///     Computes the EXP gain for items 649, 650, and 1489 using the level-band formula:
    ///     ⌈(factor2 − factor1) / 100⌉ × <paramref name="multiplier" />.
    ///     Returns zero when the level row is absent from the table (defensive floor) or the band
    ///     is non-positive.
    /// </summary>
    public static int ComputeLevelBandGain(
        short level,
        FrozenDictionary<short, LevelRowDto> levelsByLevel,
        int multiplier)
    {
        if (!levelsByLevel.TryGetValue(level, out var row))
            return 0;

        var factor1 = (long)row.ExpRangeMin;
        // At max level, legacy uses MAX_NUMBER_SIZE (2,000,000,000) as the upper bound
        // rather than the table's own ExpRangeMax — faithful to S04_MyWork03.cpp:4143-4148.
        var factor2 = level == LevelProgressionCalculator.MaxLevel
            ? HighLevelExperienceResolver.MaxMainExperience
            : (long)row.ExpRangeMax;

        var band = factor2 - factor1;
        if (band <= 0)
            return 0;

        var baseUnit = (int)((band + 99L) / 100L); // integer ceiling of (band / 100)
        return baseUnit * multiplier;
    }

    /// <summary>
    ///     Computes the EXP gain for item 1490: ⌈ReturnHighExpValue(level2) / 100⌉.
    ///     Returns zero when <paramref name="level2" /> is outside the valid rebirth-tier range 1–12.
    /// </summary>
    public static int ComputeRebirthTierGain(short level2)
    {
        if (level2 < 1 || level2 > RebirthProgression.MaxHighLevel)
            return 0;

        var threshold = (long)RebirthProgression.HighLevelExpTable[level2 - 1];
        return (int)((threshold + 99L) / 100L); // integer ceiling of (threshold / 100)
    }
}
