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
///         The whole per-zone <c>switch(tZoneNumber)</c> this reimplements sits behind
///         <c>#ifdef REGULAR_WAR_RENEWAL</c> (S07_MyGame03.cpp:2838), which is unconditionally
///         <c>#define</c>'d (<c>Server/Header/Protocol/DEFINE.h:79</c>, outside any build-variant guard) --
///         so the <c>#else</c> fallback (a bare zone-blind <c>getMaxCP</c> call, S07_MyGame03.cpp:3052-3057)
///         never compiles in any shipped configuration; the switch below is the real, live behavior.
///     </para>
///     <para>
///         <b>What this catalog does NOT yet classify, and why:</b> recovering the remaining branches'
///         exact zone-number lists and cross-cutting world-state preconditions needs live map-catalog/RvR
///         state this method has no access to (a separate, larger wiring change), so they still fall through
///         to the <c>default</c> branch below instead of their own real behavior -- a known, deliberate gap,
///         not a silent bug:
///         <list type="bullet">
///             <item>
///                 Twelve "tribe-symbol-battle" secondary zones (2/3/4/7/8/9/12/13/14/141/142/143,
///                 S07_MyGame03.cpp:2853-2886): should grant the full set (plus HSB-rank eligibility, itself
///                 dead per this class's own summary above) only while that zone's tribe-symbol-battle
///                 world flag (<c>mWorldInfo-&gt;mTribeSymbolBattle==1</c>) is active. When that flag is 0,
///                 the legacy branch that would otherwise gate a grant on kill-type != stun is itself dead in
///                 every shipped build (<c>#ifndef LNW33</c>, S07_MyGame03.cpp:2876-2884, and <c>LNW33</c> is
///                 always defined) -- so in production this half of the branch grants nothing at all,
///                 stun or not.
///             </item>
///             <item>
///                 Zone 38 ("DTM", S07_MyGame03.cpp:2887-2948): should grant CP+EXP unconditionally, with
///                 drop/HSB-eligibility/daily-mission gated by a battle-post-distance/battle-state check (when
///                 <c>mTribeSymbolBattle==0</c>) or a "Teleborn" exclusion zone (when <c>mTribeSymbolBattle==1</c>).
///                 None of case 38's active (<c>LNW33</c>-defined) branches ever check the stun/kill-type
///                 marker at all -- every stun-vs-not check inside this case sits behind the same always-dead
///                 <c>#ifndef LNW33</c> guard as above.
///             </item>
///             <item>
///                 The "regular-war-tier" zone family (49/146/149/154/157/160/120/121/122,
///                 51/147/150/155/158/161, 53/148/151/152/153/156/159/162/163/164, 295/296,
///                 S07_MyGame03.cpp:2950-2986): should grant the full set unconditionally -- no stun gate at
///                 all, unlike <c>default</c>.
///             </item>
///             <item>
///                 Two zones (195/196, S07_MyGame03.cpp:3018-3028, only live under <c>#ifdef LNW33</c>, which
///                 is always defined) gated by a live, time-windowed event flag (<c>mGAME.isZone195TimeEvent</c>):
///                 full set, no stun gate, only while that window is open; nothing at all while it is closed.
///             </item>
///         </list>
///         Zone 335 (FFA), zones 194/267/268/269, and the four "city" zones (1/6/11/140) ARE fully modeled
///         below since the contract/source states their ids and exact per-flag stun gating explicitly.
///     </para>
/// </remarks>
public static class PvpKillRewardZoneCatalog
{
    /// <summary>Server/Header/Protocol/DEFINE.h:99 (<c>FFAMAPNUM</c>).</summary>
    public const short FfaMapNumber = 335;

    /// <summary>
    ///     B9: confirmed +2 -- the FFA branch sets the hero-rank point award to 2
    ///     (S07_MyGame03.cpp:3010,3103-3104). No combined-level floor gates it (see
    ///     <see cref="HeroPointMinimumCombinedLevel" />'s own remarks).
    /// </summary>
    public const int FfaHeroPointAmount = 2;

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
    ///     The four "city" zones -- <c>case 1: case 6: case 11: case 140:</c>
    ///     (S07_MyGame03.cpp:2841-2852). Unlike <see cref="UnconditionalFullRewardZoneIds" />, only
    ///     <see cref="PvpKillZoneRewardProfile.GrantContributionPoints" /> is gated on the kill not being a
    ///     stun trigger (<c>if (tKillType != KILL_CP_TYPE::STUN) tCanGainCp = TRUE;</c>); drop/EXP/daily-mission
    ///     progress are assigned unconditionally immediately below that check, so a stun-trigger kill in one
    ///     of these zones still grants all three -- only CP is withheld.
    /// </summary>
    private static readonly short[] CityZoneIds = [1, 6, 11, 140];

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

        if (Array.IndexOf(CityZoneIds, zoneId) >= 0)
            // CP alone is withheld on a stun-trigger kill; drop/EXP/daily-mission progress are unconditional.
            return new PvpKillZoneRewardProfile(!isStunTrigger, true, true, true, 0);

        // Default branch (S07_MyGame03.cpp default case, live in the shipped build since __GOD__ is
        // unconditionally #define'd on both sides of the M33 build-variant split, DEFINE.h:24,29): full
        // reward set, gated only on the kill-type marker not being "stun" -- a stun-chain trigger grants
        // nothing at all here.
        var grantsAnything = !isStunTrigger;
        return new PvpKillZoneRewardProfile(grantsAnything, grantsAnything, grantsAnything, grantsAnything, 0);
    }
}
