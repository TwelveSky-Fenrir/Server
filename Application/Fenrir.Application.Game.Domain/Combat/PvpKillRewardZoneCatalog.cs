namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Per-zone eligibility for the five reward channels a cross-tribe PvP kill can grant, resolved by
///     <see cref="PvpKillRewardZoneCatalog.Resolve" />. Every flag here is independent -- a zone can grant a
///     strict subset of the "full" reward (drop+CP+EXP+dailyMission), see <see cref="PvpKillRewardZoneCatalog" />'s
///     own remarks for exactly which zones do.
/// </summary>
public readonly record struct PvpKillZoneRewardProfile(
    bool GrantContributionPoints,
    bool GrantExperience,
    bool GrantDrop,
    bool GrantDailyMissionProgress,
    int HeroPointAmount)
{
    public static readonly PvpKillZoneRewardProfile None = new(false, false, false, false, 0);
}

/// <summary>
///     Reimplements the per-zone-number branch of <c>MyUtil::ProcessForKillOtherTribe</c>
///     (S07_MyGame03.cpp:2602-3248) that decides which of CP/EXP/drop/daily-mission-progress/hero-point a
///     cross-tribe kill actually grants. HSB-rank-point eligibility is deliberately not modeled here: the
///     legacy grant call for that currency sits behind a feature macro (<c>USE_RANK_POINT</c>) that is
///     commented out and never redefined anywhere in the project (DEFINE.h:77), so it is dead code in every
///     shipped build -- porting it as live behavior would be inventing a currency nothing ever pays out.
/// </summary>
/// <remarks>
///     <para>
///         <b>What this catalog does NOT yet classify, and why:</b> the legacy-behavior-translator contract
///         this was built from deliberately elides the exact zone-number lists for several branches (it
///         describes their reward shape and gating in prose, not their concrete ids), and reading
///         <c>Server/ts25zone/S07_MyGame03.cpp</c>'s full switch statement to recover those ids is out of
///         this agent's scope (that is <c>cpp-zone-gameplay-analyst</c>'s job, via a follow-up
///         <c>legacy-behavior-translator</c> contract). Every zone id in one of the categories below
///         therefore falls through to the <c>default</c> branch below instead of its own real behavior --
///         a known, deliberate gap, not a silent bug:
///         <list type="bullet">
///             <item>
///                 Four "town invasion" zones: should grant drop+EXP+dailyMission unconditionally but gate
///                 CP alone on kill-type != stun (a narrower CP gate than <c>default</c>'s, which gates all
///                 four flags together).
///             </item>
///             <item>
///                 Twelve "tribe-symbol-battle" secondary zones: should grant the full set only while that
///                 zone's tribe-symbol-battle world flag (<c>WorldRvrState.TribeSymbolBattle</c> is the Fenrir
///                 equivalent) is active, and nothing at all otherwise.
///             </item>
///             <item>
///                 One "DTM" zone: should always grant CP+EXP unconditionally regardless of sub-branch,
///                 with only drop-tier/HSB-eligibility/dailyMission varying by the tribe-symbol-battle flag and a
///                 battle-post-distance/battle-state sub-check.
///             </item>
///             <item>
///                 A long list of "regular-war-tier" zones: should grant the full set unconditionally (no
///                 stun gate at all, unlike <c>default</c>).
///             </item>
///             <item>
///                 Two zones gated by a live, time-windowed event flag: full set only while that window is
///                 open.
///             </item>
///         </list>
///         Zone 335 (FFA) and zones 194/267/268/269 ARE fully modeled below since the contract states their
///         ids explicitly.
///     </para>
/// </remarks>
public static class PvpKillRewardZoneCatalog
{
    /// <summary>Server/Header/Protocol/DEFINE.h:99 (<c>FFAMAPNUM</c>).</summary>
    public const short FfaMapNumber = 335;

    /// <summary>
    ///     No legacy source value plumbed to this contract for the FFA branch's fixed hero-point amount --
    ///     "only worth exactly a fixed small amount." Placeholder, not real game-balance tuning (same posture
    ///     as <see cref="Fenrir.Application.Game.Domain.GameServerOptions.MaxPlausibleSpeedPerSecond" />).
    /// </summary>
    public const int FfaHeroPointAmount = 1;

    /// <summary>
    ///     "Gated on the attacker's combined level being at least a fixed minimum threshold." No legacy value
    ///     available to this contract; defaults to 0 (never gates) rather than guessing a threshold that
    ///     could wrongly withhold a real grant.
    /// </summary>
    public const int HeroPointMinimumCombinedLevel = 0;

    /// <summary>
    ///     "One specific zone number (194) and a group of three consecutive zone numbers (267/268/269)... each
    ///     grants the full reward set unconditionally." Live case labels per the contract's own Citations
    ///     (H07_MyGame.h:20,34 shows the <c>ZONE194</c>/<c>ZONE267</c> identifiers exist even though the
    ///     adjacent scoreboard-tally macros keyed to them are commented out/dead).
    /// </summary>
    private static readonly short[] UnconditionalFullRewardZoneIds = [194, 267, 268, 269];

    /// <summary>
    ///     Resolves the reward-eligibility profile for a kill happening in <paramref name="zoneId" />.
    ///     <paramref name="isStunTrigger" /> is the "stun vs. not" collapse of the legacy's three-value
    ///     kill-type marker (<c>KILL_CP_TYPE</c>, H07_MyGame.h:51-55) -- "normal hit" and "designated one-hit
    ///     skill" are treated identically everywhere this routine reads the marker, per the source contract.
    /// </summary>
    public static PvpKillZoneRewardProfile Resolve(short zoneId, bool isStunTrigger)
    {
        if (zoneId == FfaMapNumber)
            // CP is explicitly NOT granted here -- it flows through the dedicated FFA flat-amount override
            // instead (its own 2-minute same-pair cooldown, see PvpKillContributionPointCalculator). EXP and
            // daily-mission progress are never granted for an FFA kill reached via this branch, even though
            // ordinary town/regular-war kills do grant both.
            return new PvpKillZoneRewardProfile(
                false,
                false,
                true,
                false,
                FfaHeroPointAmount);

        if (Array.IndexOf(UnconditionalFullRewardZoneIds, zoneId) >= 0)
            return new PvpKillZoneRewardProfile(true, true, true, true, 0);

        // Default branch (S07_MyGame03.cpp default case, live in the shipped build): full reward set, gated
        // only on the kill-type marker not being "stun" -- a stun-chain trigger (mCase 5, not yet wired into
        // Fenrir's combat resolver) grants nothing at all here.
        var grantsAnything = !isStunTrigger;
        return new PvpKillZoneRewardProfile(grantsAnything, grantsAnything, grantsAnything, grantsAnything, 0);
    }
}
