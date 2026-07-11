namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     The attacker/victim level-gap scaling and the regular-war-vs-configured zone multiplier of the PvP-kill
///     EXP branch of <c>MyUtil::ProcessForKillOtherTribe</c>. Pure formula, no I/O, no state -- the middle stage
///     between <see cref="PvpKillExperienceBaseTable.Lookup" /> (the raw base) and
///     <see cref="PvpKillExperienceCalculator.ComputeGain" /> (the warrior-scroll / double-kill multipliers).
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : <c>LEVELSYSTEM::ReturnGainExpForKillOtherTribe</c>,
///         <c>Server/ts25zone/GameSystem/GameSystem_01_Level.cpp:721-735</c> for <see cref="Scale" />; the
///         times-150-vs-configured-cross-tribe-ratio choice at <c>Server/ts25zone/S07_MyGame03.cpp:3145-3154</c>
///         (<c>LNW33</c> branch; the alternate per-map times-100/150 table at
///         <c>S07_MyGame03.cpp:3113-3144</c> is dead code) for <see cref="ResolveZoneMultiplier" />.
///     </para>
///     <para>
///         <b>Float parity.</b> <see cref="Scale" /> reproduces the legacy single-precision arithmetic exactly
///         as the sibling monster-kill formula (<see cref="ExperienceFormulas.ComputeMonsterKillExperience" />)
///         does -- the down-scaling subtraction can therefore land one below the naive integer expectation at an
///         integer boundary (e.g. a base of 100 down-scaled by one level truncates to 89, not 90), which is the
///         legacy behavior, not a defect. The exact float association order follows the contract's "base plus/
///         minus base times (gap times ten percent)" phrasing; any residual difference from the legacy source's
///         own association is a sub-ULP micro-fidelity detail, never more than 1 at an integer boundary.
///     </para>
/// </remarks>
public static class PvpKillExperienceScaling
{
    /// <summary>
    ///     Ten percent of the base per level of gap -- the up-scaling bonus / down-scaling penalty step.
    /// </summary>
    public const float PerLevelScaleStep = 0.1f;

    /// <summary>
    ///     Zeroed out when the attacker's combined level exceeds the victim's by more than this -- the tight
    ///     inner EXP gate (9), strictly tighter than the outer 13-level anti-gank gate applied upstream in the
    ///     reward pipeline, so an attacker 10-13 combined levels above the victim still earns CP/drops but zero
    ///     base EXP.
    /// </summary>
    public const int UnfavorableLevelGapZeroThreshold = 9;

    /// <summary>The regular-war-type map EXP multiplier (times 150), <c>S07_MyGame03.cpp:3145-3154</c>.</summary>
    public const int RegularWarXpMultiplier = 150;

    /// <summary>
    ///     Scales <paramref name="baseXp" /> (the victim-combined-level lookup from
    ///     <see cref="PvpKillExperienceBaseTable" />) by the attacker/victim combined-level gap:
    ///     up-scaled by ten percent per level when the attacker is below the victim, down-scaled by ten percent
    ///     per level when at or above -- truncated toward zero. Returns 0 when either combined level is outside
    ///     <see cref="PvpKillExperienceBaseTable.MinCombinedLevel" />..
    ///     <see cref="PvpKillExperienceBaseTable.MaxCombinedLevel" />, or when the attacker exceeds the victim by
    ///     more than <see cref="UnfavorableLevelGapZeroThreshold" />.
    /// </summary>
    public static int Scale(int baseXp, int attackerCombinedLevel, int victimCombinedLevel)
    {
        if (attackerCombinedLevel is < PvpKillExperienceBaseTable.MinCombinedLevel
            or > PvpKillExperienceBaseTable.MaxCombinedLevel)
            return 0;

        if (victimCombinedLevel is < PvpKillExperienceBaseTable.MinCombinedLevel
            or > PvpKillExperienceBaseTable.MaxCombinedLevel)
            return 0;

        if (attackerCombinedLevel - victimCombinedLevel > UnfavorableLevelGapZeroThreshold)
            return 0;

        if (attackerCombinedLevel < victimCombinedLevel)
        {
            var favorableGap = victimCombinedLevel - attackerCombinedLevel;
            return (int)(baseXp + baseXp * (favorableGap * PerLevelScaleStep));
        }

        var unfavorableGap = attackerCombinedLevel - victimCombinedLevel;
        return (int)(baseXp - baseXp * (unfavorableGap * PerLevelScaleStep));
    }

    /// <summary>
    ///     The zone EXP multiplier applied after <see cref="Scale" />: <see cref="RegularWarXpMultiplier" /> (150)
    ///     on a regular-war-type map (the attacker's server is one of the regular-war server numbers), otherwise
    ///     the deployment-configured cross-tribe XP ratio (<paramref name="crossTribeXpRatio" />; shipped
    ///     BuildEU33 value 2, but operator-configurable -- do not hardcode it). Callers pass the result as the
    ///     <c>zoneMultiplier</c> of <see cref="PvpKillExperienceCalculator.ComputeGain" />.
    /// </summary>
    public static int ResolveZoneMultiplier(bool isRegularWarServer, int crossTribeXpRatio)
    {
        return isRegularWarServer ? RegularWarXpMultiplier : crossTribeXpRatio;
    }
}
