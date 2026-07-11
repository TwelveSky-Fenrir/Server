namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     The base PvP-kill EXP amount looked up by the VICTIM's combined level (Level + Level2), the first term of
///     the PvP-kill EXP branch of <c>MyUtil::ProcessForKillOtherTribe</c> before
///     <see cref="PvpKillExperienceScaling.Scale" /> applies the attacker/victim level-gap scaling and before
///     the <see cref="PvpKillExperienceCalculator" /> zone/warrior-scroll/double-kill multipliers. Pure data +
///     lookup, no I/O, no state.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : the production <c>LNW33</c> branch of the EXP-per-victim-level table,
///         <c>Server/ts25zone/GameSystem/GameSystem_01_Level.cpp:159-318</c> (its constructor zero-fills all 157
///         slots first, <c>GameSystem_01_Level.cpp:9-12</c>). This closes the exact gap
///         <see cref="PvpKillExperienceCalculator.PlaceholderBaseAmountPerKill" /> was standing in for: that
///         placeholder was a flat 50 because "no per-defender-level EXP table was given to the source contract"
///         -- the B9 contract now supplies the real table, so this type is what should feed
///         <c>ComputeGain(baseAmount: ...)</c> instead of the placeholder.
///     </para>
///     <para>
///         <b>Production-only table.</b> In every shipped build every combined level 1-145 yields a flat
///         <see cref="LowTierBase" /> (110); the tiered 40/50/60/.../110 table in the alternate branch is dead
///         code (<c>GameSystem_01_Level.cpp:13-158</c>, non-<c>LNW33</c>). The high-level tier
///         (146-157 -> 330/360/390) is set unconditionally and is identical in both build branches
///         (<c>GameSystem_01_Level.cpp:307-318</c>). Any combined level outside 1-157 returns 0, matching the
///         constructor's zero-fill for slots the table never assigns.
///     </para>
/// </remarks>
public static class PvpKillExperienceBaseTable
{
    /// <summary>Lowest valid combined level -- table slots are indexed by (combined level - 1).</summary>
    public const int MinCombinedLevel = 1;

    /// <summary>
    ///     Highest valid combined level: 145 base-level slots plus 12 high-level slots
    ///     (<c>Server/Header/Protocol/DEFINE.h:604-605</c>).
    /// </summary>
    public const int MaxCombinedLevel = 157;

    /// <summary>Combined levels 1-145 (<c>GameSystem_01_Level.cpp:159-305</c>, <c>LNW33</c> branch).</summary>
    public const int LowTierBase = 110;

    /// <summary>Combined levels 146-149 (<c>GameSystem_01_Level.cpp:307-318</c>).</summary>
    public const int HighTier1Base = 330;

    /// <summary>Combined levels 150-153 (<c>GameSystem_01_Level.cpp:307-318</c>).</summary>
    public const int HighTier2Base = 360;

    /// <summary>Combined levels 154-157 (<c>GameSystem_01_Level.cpp:307-318</c>).</summary>
    public const int HighTier3Base = 390;

    // Indexed by (combined level - 1); slot 0 == combined level 1. Built once at type init, never mutated,
    // read O(1) with no hashing (dense small-integer key -> plain array, not a dictionary, per the house
    // "index a dense range directly" idiom).
    private static readonly int[] BaseByCombinedLevelIndex = BuildTable();

    private static int[] BuildTable()
    {
        var table = new int[MaxCombinedLevel];

        for (var i = 0; i < 145; i++)
            table[i] = LowTierBase; // combined levels 1-145

        for (var i = 145; i < 149; i++)
            table[i] = HighTier1Base; // combined levels 146-149

        for (var i = 149; i < 153; i++)
            table[i] = HighTier2Base; // combined levels 150-153

        for (var i = 153; i < 157; i++)
            table[i] = HighTier3Base; // combined levels 154-157

        return table;
    }

    /// <summary>
    ///     The base PvP-kill EXP amount for a victim at <paramref name="victimCombinedLevel" /> (the victim's
    ///     Level + Level2). Returns 0 for any combined level outside <see cref="MinCombinedLevel" />..
    ///     <see cref="MaxCombinedLevel" />, matching the legacy table's zero-filled unassigned slots.
    /// </summary>
    public static int Lookup(int victimCombinedLevel)
    {
        if (victimCombinedLevel is < MinCombinedLevel or > MaxCombinedLevel)
            return 0;

        return BaseByCombinedLevelIndex[victimCombinedLevel - 1];
    }
}
