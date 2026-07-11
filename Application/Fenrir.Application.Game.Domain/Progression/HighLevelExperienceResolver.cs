using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Which branch of the post-cap experience routine a single awarded-experience amount resolves to. A
///     hand-discriminated result, not a class hierarchy -- only <see cref="HighLevelExperienceOutcome.Kind" />
///     decides which of the outcome's fields are meaningful (same convention as
///     <c>World.ZoneCommand</c>).
/// </summary>
public enum HighLevelExperienceOutcomeKind : byte
{
    /// <summary>Silent no-op: below-1 gain, anti-cheat flag, not at the general-level cap, or the cap already fully saturated.</summary>
    None = 0,

    /// <summary>
    ///     Stage A -- general-level cap reached but the general-experience pool not yet saturated: fill the pool
    ///     toward the ceiling and grant one stat point per whole percent of the pool's band crossed.
    /// </summary>
    MainPoolFill = 1,

    /// <summary>
    ///     Stage B -- a rebirth-tier (<c>aLevel2</c>) level-up: +100 skill, pool reset, full ability recompute + heal,
    ///     discriminator-11 broadcast.
    /// </summary>
    RebirthTierLevelUp = 2,

    /// <summary>
    ///     Stage B -- rebirth-tier proportional accumulation: pool grows, stat points granted per whole percent of the
    ///     current level's band crossed.
    /// </summary>
    RebirthTierAccrual = 3
}

/// <summary>
///     The server-authoritative inputs to <see cref="HighLevelExperienceResolver.Resolve" />. Every field is a
///     value already held on the awarding character's own <c>PlayerRuntimeState</c> (or read from world
///     reference data) -- nothing here is client-supplied, so no field needs its own trust/clamp gate beyond
///     what the resolver already applies.
/// </summary>
/// <param name="Level">aLevel1 -- the character's current general level.</param>
/// <param name="MainExperience">aExp1 -- the current general-experience pool.</param>
/// <param name="MainExperienceFloor">
///     The general-level cap's own experience floor (the level-<see cref="LevelProgressionCalculator.MaxLevel" />
///     row's <c>ExpRangeMin</c>), used only as the lower endpoint of Stage A's percentage band. Pass 0 when the
///     cap row is unavailable.
/// </param>
/// <param name="Level2">aLevel2 -- the current rebirth-tier level (0-12).</param>
/// <param name="Exp2">aExp2 -- the current rebirth-tier experience pool.</param>
/// <param name="AwardedExperience">
///     The original, un-clamped awarded experience amount handed off from the general award
///     routine.
/// </param>
/// <param name="AntiCheatExperienceFlagged">
///     The per-session anti-cheat experience flag. When set, the awarded amount is forced to zero on entry to
///     the Stage B routine (a flagged session earns no rebirth-tier experience). Honored in Stage B only -- the
///     equivalent gate on the general-experience path is compiled out of the shipped build.
/// </param>
public readonly record struct HighLevelExperienceInput(
    short Level,
    long MainExperience,
    long MainExperienceFloor,
    short Level2,
    int Exp2,
    int AwardedExperience,
    bool AntiCheatExperienceFlagged = false);

/// <summary>
///     What one <see cref="HighLevelExperienceResolver.Resolve" /> call decided. The caller applies exactly the
///     fields <see cref="Kind" /> marks meaningful (see each field) -- see <c>Zone.HighLevelExperience.cs</c>'s
///     apply/broadcast wiring.
/// </summary>
public readonly record struct HighLevelExperienceOutcome
{
    /// <summary>Which branch fired -- the discriminator for every other field on this outcome.</summary>
    public required HighLevelExperienceOutcomeKind Kind { get; init; }

    /// <summary>
    ///     Meaningful only for <see cref="HighLevelExperienceOutcomeKind.MainPoolFill" />: the new (clamped)
    ///     general-experience pool value to store.
    /// </summary>
    public long NewMainExperience { get; init; }

    /// <summary>
    ///     Meaningful only for <see cref="HighLevelExperienceOutcomeKind.RebirthTierLevelUp" />: the new rebirth-tier
    ///     level (old + 1).
    /// </summary>
    public short NewLevel2 { get; init; }

    /// <summary>
    ///     Meaningful only for <see cref="HighLevelExperienceOutcomeKind.RebirthTierAccrual" />: the new rebirth-tier
    ///     experience pool value to store. (A level-up resets the pool to 0.)
    /// </summary>
    public int NewExp2 { get; init; }

    /// <summary>
    ///     Meaningful only for <see cref="HighLevelExperienceOutcomeKind.RebirthTierLevelUp" />: skill points to add
    ///     (always <see cref="HighLevelExperienceResolver.RebirthTierLevelUpSkillPoints" />).
    /// </summary>
    public int SkillPointsGranted { get; init; }

    /// <summary>
    ///     Meaningful for <see cref="HighLevelExperienceOutcomeKind.MainPoolFill" /> and
    ///     <see cref="HighLevelExperienceOutcomeKind.RebirthTierAccrual" />: stat points to add (0-100, one per whole percent
    ///     of the band crossed).
    /// </summary>
    public int StatPointsGranted { get; init; }

    /// <summary>
    ///     Meaningful only for <see cref="HighLevelExperienceOutcomeKind.RebirthTierLevelUp" /> on the 0-to-1 transition:
    ///     the "zone-101 time" counter increment (else 0).
    /// </summary>
    public int Zone101TimeBonus { get; init; }

    public static HighLevelExperienceOutcome None => new() { Kind = HighLevelExperienceOutcomeKind.None };

    public static HighLevelExperienceOutcome MainPoolFill(long newMainExperience, int statPointsGranted)
    {
        return new HighLevelExperienceOutcome
        {
            Kind = HighLevelExperienceOutcomeKind.MainPoolFill,
            NewMainExperience = newMainExperience,
            StatPointsGranted = statPointsGranted
        };
    }

    public static HighLevelExperienceOutcome LevelUp(short newLevel2, int skillPointsGranted, int zone101TimeBonus)
    {
        return new HighLevelExperienceOutcome
        {
            Kind = HighLevelExperienceOutcomeKind.RebirthTierLevelUp,
            NewLevel2 = newLevel2,
            NewExp2 = 0,
            SkillPointsGranted = skillPointsGranted,
            Zone101TimeBonus = zone101TimeBonus
        };
    }

    public static HighLevelExperienceOutcome Accrual(int newExp2, int statPointsGranted)
    {
        return new HighLevelExperienceOutcome
        {
            Kind = HighLevelExperienceOutcomeKind.RebirthTierAccrual,
            NewExp2 = newExp2,
            StatPointsGranted = statPointsGranted
        };
    }
}

/// <summary>
///     Pure port of the legacy post-cap experience routine -- the two-stage fork <c>MyUtil::ProcessForExperience</c>
///     applies once a character's general level is already at its hard cap (Stage A, filling the general-experience
///     pool toward its ceiling), and the dedicated high-level routine <c>ProcessForExperience2</c> it hands off to
///     once that pool is saturated (Stage B, the rebirth-tier <c>aLevel2</c> ladder). No I/O or state dependency:
///     every input is a value the caller already holds, every result is a decision the caller applies.
///     <para>
///         This is the "distinct, unimplemented prerequisite subsystem" both
///         <see cref="RebirthProgression" />'s and <see cref="LevelProgressionCalculator" />'s own remarks flag as
///         missing -- the only mechanism that raises <c>PlayerRuntimeState.Level2</c> from 0 toward
///         <see cref="RebirthProgression.MaxHighLevel" /> and feeds the <c>Level2 == 12 + pool full</c>
///         precondition the two rebirth-generation paths (tribe-work "Max Rebirth" and the Rebirth-Pill item)
///         require. It does NOT touch <c>aRebirthNum</c> (rebirth generation) -- that is those two paths' concern,
///         not this routine's.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ (per the B17-rebirth behavior contract, mechanic (a)) :
///     Server/ts25zone/S07_MyGame03.cpp:196-227 (the level-cap two-stage fork: saturated-pool handoff vs. general-
///     pool proportional accumulation), :324-424 (the high-level routine body), :333-338 (live anti-cheat flag +
///     below-1 guard), :341 (entry guard: pool saturated and rebirth-tier level at/below cap), :347-351 (global
///     clamp of the gain to the level-12 ceiling), :358-404 (per-current-level threshold, the level switch, the
///     level-0..11 level-up block and the level-12 cap block), :379-395 (zone-101 time +10000 on the 0-to-1
///     transition under MG5ORIGIN; +100 skill; pool reset; level increment; recompute + heal; discriminator-11
///     broadcast), :405-421 (proportional stat-point gain and pool accumulation with the in-level clamp) ;
///     Server/ts25zone/GameSystem/GameSystem_01_Level.cpp:319-330 (the twelve rebirth-tier thresholds, sourced via
///     <see cref="RebirthProgression.HighLevelExpTable" />), :712-719 (threshold lookup: zero if level outside
///     1-12) ; Server/Header/Protocol/DEFINE.h:18 (MG5ORIGIN, defined unconditionally -- the zone-101 bonus is
///     always live), :365 (MAX_NUMBER_SIZE = 2,000,000,000, the general-pool saturation ceiling), :604-605
///     (general-level cap 145, rebirth-tier cap 12).
/// </remarks>
public static class HighLevelExperienceResolver
{
    /// <summary>
    ///     MAX_NUMBER_SIZE (DEFINE.h:365) -- the general-experience (aExp1) saturation ceiling. Once the general
    ///     pool reaches this at the level cap, every further award routes to the rebirth-tier ladder instead.
    /// </summary>
    public const long MaxMainExperience = 2_000_000_000L;

    /// <summary>
    ///     The global rebirth-tier experience ceiling -- the level-12 threshold (equal to
    ///     <c>RebirthProgression.HighLevelExpTable[MaxHighLevel - 1]</c>; a drift guard asserts the equality).
    ///     Every awarded gain is clamped so the pool can never exceed this.
    /// </summary>
    public const int RebirthTierExperienceCeiling = 1_481_117_817;

    /// <summary>Skill points granted on every rebirth-tier level-up (S07_MyGame03.cpp:388).</summary>
    public const int RebirthTierLevelUpSkillPoints = 100;

    /// <summary>
    ///     The "zone-101 time" counter increment granted once, on the rebirth-tier 0-to-1 transition
    ///     (S07_MyGame03.cpp:383, under MG5ORIGIN).
    /// </summary>
    public const int Zone101TimeGrantOnFirstRebirthTier = 10_000;

    /// <summary>
    ///     Whether this routine governs experience awards for a character at the given general level. Below the
    ///     general-level cap the ordinary <see cref="LevelProgressionCalculator" /> path applies instead; at (or,
    ///     defensively, above) it, awards route here. The routing call site is <c>Zone.ApplyCharacterExperienceGain</c>.
    /// </summary>
    public static bool AppliesAt(short level)
    {
        return level >= LevelProgressionCalculator.MaxLevel;
    }

    /// <summary>
    ///     Resolves a single awarded-experience amount for a character already at the general-level cap into one
    ///     of four outcomes (see <see cref="HighLevelExperienceOutcomeKind" />). Returns
    ///     <see cref="HighLevelExperienceOutcome.None" /> unchanged for a below-cap character, so the caller can
    ///     safely fork on <see cref="AppliesAt" /> and hand every at-cap award here.
    /// </summary>
    public static HighLevelExperienceOutcome Resolve(in HighLevelExperienceInput input)
    {
        // Below the general-level cap: this routine does not apply -- the caller runs the ordinary level-up path.
        if (input.Level < LevelProgressionCalculator.MaxLevel)
            return HighLevelExperienceOutcome.None;

        // Stage A -- general cap reached but the general-experience pool not yet saturated. The anti-cheat gate
        // is deliberately NOT applied here: its equivalent on the general-experience path is compiled out of the
        // shipped build (dead), so a flagged session still fills the general pool normally.
        if (input.MainExperience < MaxMainExperience)
            return ResolveMainPoolFill(input.MainExperience, input.MainExperienceFloor, input.AwardedExperience);

        // Stage B -- general pool saturated: the dedicated high-level routine (ProcessForExperience2).

        // Anti-cheat flag forces the awarded amount to zero on entry, ahead of the below-1 guard. This gate IS
        // live in the high-level routine (unlike the general-experience path above).
        var gain = input.AntiCheatExperienceFlagged ? 0 : input.AwardedExperience;

        // Below-1 gain ends the routine (silent no-op).
        if (gain < 1)
            return HighLevelExperienceOutcome.None;

        // Entry guard: the rebirth-tier level must already be at or below its cap.
        if (input.Level2 > RebirthProgression.MaxHighLevel)
            return HighLevelExperienceOutcome.None;

        // Global clamp of the gain to the level-12 ceiling, applied before the per-level switch every tick.
        if (input.Exp2 + (long)gain > RebirthTierExperienceCeiling)
            gain = RebirthTierExperienceCeiling - input.Exp2;
        if (gain < 0)
            gain = 0;

        var threshold = RebirthTierThreshold(input.Level2);

        // Level-up branch: rebirth-tier levels 0..11 whose pool already meets the current level's threshold. The
        // level-0 threshold is zero, so a level-0 character always qualifies on its first tick. The triggering
        // gain is discarded (the pool resets to zero on level-up).
        if (input.Level2 < RebirthProgression.MaxHighLevel && input.Exp2 >= threshold)
        {
            var zone101 = input.Level2 == 0 ? Zone101TimeGrantOnFirstRebirthTier : 0;
            return HighLevelExperienceOutcome.LevelUp((short)(input.Level2 + 1), RebirthTierLevelUpSkillPoints,
                zone101);
        }

        // Accumulation branch (levels 1..11 whose pool is still below the threshold, or the level-12 cap). Clamp
        // the gain to the current level's threshold, then grant one stat point per whole percentage point of the
        // level's band this gain crosses.
        if (input.Exp2 + (long)gain > threshold)
            gain = threshold - input.Exp2;
        if (gain < 1)
            return HighLevelExperienceOutcome.None;

        var presentPercent = PercentOfThreshold(input.Exp2, threshold);
        var newExp2 = input.Exp2 + gain;
        var nextPercent = PercentOfThreshold(newExp2, threshold);
        var statPoints = nextPercent - presentPercent;
        if (statPoints < 0)
            statPoints = 0;

        return HighLevelExperienceOutcome.Accrual(newExp2, statPoints);
    }

    /// <summary>
    ///     The rebirth-tier experience threshold for a given level: zero for level 0 (and any level outside
    ///     1-12), else the level's entry in <see cref="RebirthProgression.HighLevelExpTable" />. Mirrors
    ///     <c>LEVELSYSTEM::ReturnHighExpValue</c> (GameSystem_01_Level.cpp:712-719).
    /// </summary>
    public static int RebirthTierThreshold(short level2)
    {
        if (level2 <= 0)
            return 0;
        if (level2 > RebirthProgression.MaxHighLevel)
            return RebirthTierExperienceCeiling;
        return RebirthProgression.HighLevelExpTable[level2 - 1];
    }

    // Stage A: fill the general-experience pool toward its ceiling, granting one stat point per whole percentage
    // point of the [floor, ceiling] band this gain crosses. The exact band endpoints and integer/float form of
    // the legacy percentage arithmetic are abstracted by the contract (zero-code rule) -- see this file's own
    // openQuestion note; a whole-percent band-crossing model over the level-cap row's [ExpRangeMin, 2e9] band is
    // the interpretation used here.
    private static HighLevelExperienceOutcome ResolveMainPoolFill(long mainExperience, long mainExperienceFloor,
        int gain)
    {
        if (gain < 1)
            return HighLevelExperienceOutcome.None;

        var newMain = mainExperience + gain;
        if (newMain > MaxMainExperience)
            newMain = MaxMainExperience;

        var statPoints = 0;
        var band = MaxMainExperience - mainExperienceFloor;
        if (band > 0)
        {
            var presentPercent = (int)((mainExperience - mainExperienceFloor) * 100 / band);
            var nextPercent = (int)((newMain - mainExperienceFloor) * 100 / band);
            statPoints = nextPercent - presentPercent;
            if (statPoints < 0)
                statPoints = 0;
        }

        return HighLevelExperienceOutcome.MainPoolFill(newMain, statPoints);
    }

    // Whole percentage points of the given rebirth-tier threshold that the pool value represents. Long arithmetic
    // avoids the int overflow a naive value*100 would hit (thresholds reach ~1.48e9).
    private static int PercentOfThreshold(int exp2, int threshold)
    {
        if (threshold <= 0)
            return 0;
        return (int)((long)exp2 * 100 / threshold);
    }
}
