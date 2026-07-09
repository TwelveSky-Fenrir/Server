namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Pure PvP-kill EXP-gain formula (<c>MyUtil::ProcessForKillOtherTribe</c>'s EXP branch,
///     S07_MyGame03.cpp:2602-3248, base table lookup per
///     <c>LEVELSYSTEM::ReturnGainExpForKillOtherTribe</c>/GameSystem_01_Level.cpp:721-735). No I/O, no state.
/// </summary>
/// <remarks>
///     <b>Not computed from real data:</b> the base per-kill amount is supposed to be looked up from a table
///     indexed by the defender's combined level, and then scaled by a zone-dependent multiplier table --
///     neither table's values were given to the source contract (recovering them needs a
///     <c>cpp-zone-gameplay-analyst</c> finding, out of this agent's scope). <see cref="ComputeGain" /> takes
///     the pre-looked-up base amount and zone multiplier as caller-supplied parameters instead of embedding
///     invented numbers; <see cref="PlaceholderBaseAmountPerKill" /> is what
///     <see cref="Fenrir.Application.Game.Domain.World.Zone" /> passes today until that table exists.
/// </remarks>
public static class PvpKillExperienceCalculator
{
    /// <summary>
    ///     "Zeroed out entirely if the attacker's combined level exceeds the defender's by more than 9" -- a
    ///     tighter gate than the top-level 13-level Preconditions cutoff, literal value given by the source
    ///     contract.
    /// </summary>
    public const int UnfavorableLevelGapZeroThreshold = 9;

    /// <summary>"Doubled if the attacker holds a 'warrior scroll' buff" -- literal multiplier given by the source contract.</summary>
    public const int WarriorScrollMultiplier = 2;

    /// <summary>
    ///     "Multiplied by 8 if the attacker holds a finite 'double EXP' charge" -- literal multiplier given by
    ///     the source contract.
    /// </summary>
    public const int DoubleExpChargeMultiplier = 8;

    /// <summary>
    ///     No per-defender-level EXP table was given to the source contract -- see class remarks. Flat
    ///     placeholder, not real game-balance tuning.
    /// </summary>
    public const int PlaceholderBaseAmountPerKill = 50;

    /// <summary>No per-zone multiplier table was given to the source contract -- 1.0 (no scaling) until it exists.</summary>
    public const float DefaultZoneMultiplier = 1.0f;

    /// <summary>
    ///     <paramref name="baseAmount" /> (already looked up for the defender's combined level by the caller)
    ///     scaled by <paramref name="zoneMultiplier" />, doubled for a warrior-scroll buff, then multiplied by
    ///     8 for a double-EXP charge -- zeroed entirely first if
    ///     <paramref name="attackerCombinedLevel" /> - <paramref name="defenderCombinedLevel" /> exceeds
    ///     <see cref="UnfavorableLevelGapZeroThreshold" />.
    /// </summary>
    public static int ComputeGain(
        int baseAmount,
        int attackerCombinedLevel,
        int defenderCombinedLevel,
        bool hasWarriorScrollBuff,
        bool hasDoubleExpCharge,
        float zoneMultiplier = DefaultZoneMultiplier)
    {
        if (baseAmount <= 0)
            return 0;

        if (attackerCombinedLevel - defenderCombinedLevel > UnfavorableLevelGapZeroThreshold)
            return 0;

        var amount = (int)(baseAmount * zoneMultiplier);

        if (hasWarriorScrollBuff)
            amount *= WarriorScrollMultiplier;
        if (hasDoubleExpCharge)
            amount *= DoubleExpChargeMultiplier;

        return amount;
    }
}
