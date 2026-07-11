using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The fully-specified, server-authoritative numeric tables and cadence constants for the Zone175
///     "Labyrinth" 5-wave PvE mission. Every value here is either directly cited from the source behavior
///     contract or derived from a cited legacy-tick constant; the two genuinely-unrecoverable pieces (the
///     per-rebirth-tier experience amounts and the per-stage item-drop lists) are represented as clearly-flagged
///     placeholders, never invented -- see <see cref="WaveClearExperience" /> and this class's own remarks.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/ts25zone/S07_MyGame01.cpp:8609-8745</c> (reward routine: fixed 100M/200M money
///     table, per-stage CP 20/20/50/100/200, over-cap skip, boss-damage reset), <c>:9313-9372</c>
///     (rebirth-tier experience table -- values not reproduced in the contract), <c>:8916-9205</c> (inter-wave
///     depth gates keyed on <c>index2</c>), <c>Server/ts25zone/S07_MyGame02.cpp:2394-2416</c> (wave-boss special
///     types 40-44).
/// </remarks>
public static class Zone175RewardTables
{
    /// <summary>Five waves per mission (<c>S07_MyGame01.cpp:8842-9287</c>).</summary>
    public const int WaveCount = 5;

    /// <summary>The pre-open countdown value set at open (<c>S07_MyGame01.cpp:8782-8812</c>).</summary>
    public const int PreOpenCountStart = 10;

    /// <summary>
    ///     Lowest wave-boss special type. Wave <c>N</c> (1-based) summons special type
    ///     <c>FirstWaveBossSpecialType + N - 1</c>, i.e. 40-44 (<c>S07_MyGame02.cpp:2394-2416</c>).
    /// </summary>
    public const byte FirstWaveBossSpecialType = 40;

    /// <summary>Highest wave-boss special type (wave 5).</summary>
    public const byte LastWaveBossSpecialType = 44;

    /// <summary>
    ///     Trickle-monster summon cadence during combat: one summon every 20 sub-ticks (<c>S07_MyGame01.cpp:8886-8889</c>
    ///     ).
    /// </summary>
    public const int TrickleCadenceSubTicks = 20;

    /// <summary>
    ///     One real minute in legacy ticks -- reused from <see cref="SimulationClock.PlayTimeAccrualLegacyTicks" />
    ///     (120 ticks at TimeLogic=500 ms). The mission's own sub-tick/minute conversion helper is flagged
    ///     "not observed in the cited range" by the source contract, so this is Fenrir's grounded stand-in for
    ///     "one minute," not a byte-exact legacy constant.
    /// </summary>
    public const int OneMinuteLegacyTicks = SimulationClock.PlayTimeAccrualLegacyTicks;

    /// <summary>Pre-open countdown decrement cadence: once per one-minute cadence (<c>S07_MyGame01.cpp:8797-8812</c>).</summary>
    public const int PreOpenCountdownCadenceTicks = OneMinuteLegacyTicks;

    /// <summary>Per-wave combat timeout: a fixed 60-minute mark aborts the wave (<c>S07_MyGame01.cpp:8878-8885</c>).</summary>
    public const int WaveTimeoutLegacyTicks = 60 * OneMinuteLegacyTicks;

    /// <summary>
    ///     Terminal-state hold before force-disconnecting everyone: a fixed 60 minutes (<c>S07_MyGame01.cpp:9288-9308</c>
    ///     ).
    /// </summary>
    public const int TerminalHoldLegacyTicks = 60 * OneMinuteLegacyTicks;

    /// <summary>
    ///     Per-rebirth-tier wave-clear base experience (<c>S07_MyGame01.cpp:9313-9372</c>), indexed by rebirth
    ///     tier 0-12.
    ///     <b>
    ///         GAP -- the concrete per-tier amounts are NOT reproduced in the source behavior
    ///         contract
    ///     </b>
    ///     (its citation of the table sits in the "carried forward, not independently reopened"
    ///     set), so every entry is a flagged 0 placeholder here rather than an invented value. Tier 0 is
    ///     genuinely 0 (the legacy table only has entries for tiers 1-12), which is the one entry that is
    ///     correct today. Until a <c>cpp-zone-gameplay-analyst</c> follow-up recovers the 1-12 values, the
    ///     experience half of the reward is a no-op (see <see cref="WaveClearExperience" />).
    /// </summary>
    private static readonly ImmutableArray<long> WaveClearBaseExperience =
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    ///     The fixed per-stage money credit (<c>S07_MyGame01.cpp:8645-8672</c>): 100,000,000 for stages 1-4,
    ///     200,000,000 for stage 5. This fixed table unconditionally overwrites both the level-derived money and
    ///     the money-ratio multiplication computed just before it, so neither the level table nor the configured
    ///     money ratio has any effect on the credited amount.
    /// </summary>
    public static long MoneyForStage(int stage)
    {
        return stage >= WaveCount ? 200_000_000L : 100_000_000L;
    }

    /// <summary>
    ///     The fixed per-stage "CP" (contribution point) award (<c>S07_MyGame01.cpp:8734-8741</c>):
    ///     20 / 20 / 50 / 100 / 200 for stages 1-5.
    /// </summary>
    public static int ContributionPointsForStage(int stage)
    {
        return stage switch
        {
            1 => 20,
            2 => 20,
            3 => 50,
            4 => 100,
            5 => 200,
            _ => 0
        };
    }

    /// <summary>The wave-boss special type for a 1-based <paramref name="stage" /> (40-44).</summary>
    public static byte WaveBossSpecialType(int stage)
    {
        return (byte)(FirstWaveBossSpecialType + stage - 1);
    }

    /// <summary>Whether <paramref name="specialType" /> is one of the five wave-boss special types (40-44).</summary>
    public static bool IsWaveBossSpecialType(byte specialType)
    {
        return specialType is >= FirstWaveBossSpecialType and <= LastWaveBossSpecialType;
    }

    /// <summary>
    ///     The inter-wave depth gate (<c>S07_MyGame01.cpp:8916-9205</c>): after clearing wave
    ///     <paramref name="clearedWave" />, progression to the next wave requires the instance's configured
    ///     <paramref name="index2" /> to be at least <paramref name="clearedWave" /> (entering wave 2 needs
    ///     <c>index2 &gt;= 1</c>, wave 3 needs <c>&gt;= 2</c>, wave 4 needs <c>&gt;= 3</c>, wave 5 needs
    ///     <c>&gt;= 4</c>). Never consulted after clearing wave 5 (that path always ends the mission).
    /// </summary>
    public static bool CanAdvanceToNextWave(int clearedWave, int index2)
    {
        return index2 >= clearedWave;
    }

    /// <summary>
    ///     Wave-clear experience for a player's <paramref name="rebirthTier" /> scaled by the configured
    ///     <paramref name="experienceRatio" /> (<c>S07_MyGame01.cpp:8639-8666</c>). Tier 0 (and any tier outside
    ///     1-12) yields 0. <b>Returns 0 for every tier today</b> because the base-amount table is an
    ///     unrecovered GAP -- see <see cref="WaveClearBaseExperience" />.
    /// </summary>
    public static long WaveClearExperience(int rebirthTier, float experienceRatio)
    {
        if (rebirthTier <= 0 || rebirthTier >= WaveClearBaseExperience.Length)
            return 0;

        var baseExp = WaveClearBaseExperience[rebirthTier];
        if (baseExp <= 0 || experienceRatio <= 0f)
            return 0;

        return (long)(baseExp * experienceRatio);
    }
}
