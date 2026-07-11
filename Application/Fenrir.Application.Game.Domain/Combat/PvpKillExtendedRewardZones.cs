using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     The runtime world/event toggles the extended per-zone reward branches
///     (<see cref="PvpKillExtendedRewardZones" />) gate on. Every flag is a live world-state value the B9
///     contract flags as NOT statically recoverable from the source -- each defaults to the "inactive" reading
///     so an unwired caller withholds the gated reward rather than granting it on stale/absent state.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>mWorldInfo-&gt;mTribeSymbolBattle</c> (<c>S07_MyGame03.cpp:2865</c>) for
///     <see cref="SymbolBattleActive" />; <c>mGAME.isZone195TimeEvent</c> (<c>S07_MyGame03.cpp:3020</c>) for
///     <see cref="Map195TimeEventActive" />; the map-38 battle-post-distance / battle-state branch
///     (<c>S07_MyGame03.cpp:2887-2948</c>) for <see cref="Map38DropEnabled" />/
///     <see cref="Map38DailyMissionEnabled" /> -- the map-38 XP and CP flags are force-enabled regardless of that
///     branch, so they are NOT part of this record; only the branch-dependent drop and daily-mission flags are.
/// </remarks>
public readonly record struct PvpKillRewardZoneRuntimeState(
    bool SymbolBattleActive = false,
    bool Map195TimeEventActive = false,
    bool Map38DropEnabled = false,
    bool Map38DailyMissionEnabled = false)
{
    /// <summary>All toggles off -- the reading an unwired caller uses until real world-state is threaded in.</summary>
    public static readonly PvpKillRewardZoneRuntimeState Inactive = default;
}

/// <summary>
///     The per-zone reward-eligibility branches of <c>MyUtil::ProcessForKillOtherTribe</c>
///     (<c>Server/ts25zone/S07_MyGame03.cpp:2838-3043</c>, the production <c>REGULAR_WAR_RENEWAL</c> switch)
///     that <see cref="PvpKillRewardZoneCatalog" /> deliberately left falling through to its <c>default</c>
///     branch because the earlier finding lacked their exact zone lists and world-state preconditions. The B9
///     contract now supplies both, so this type resolves them explicitly. It <b>composes over</b>
///     <see cref="PvpKillRewardZoneCatalog" /> rather than duplicating it: <see cref="TryResolve" /> returns a
///     profile only for the four families modeled here (symbol-battle zones, map 38 "DTM", the regular-war-drop
///     zone family, and maps 195/196) and <c>null</c> for every other zone, which the caller resolves through
///     <see cref="PvpKillRewardZoneCatalog.Resolve" /> exactly as before (FFA-335, the four city zones,
///     194/267/268/269, and the catch-all default all still live there). Pure, no I/O, no state.
/// </summary>
/// <remarks>
///     <para>
///         None of these four families consult the stun-vs-not kill marker: every stun/kill-type check inside
///         these cases sits behind the always-dead <c>#ifndef LNW33</c> guard, so in production a stun-trigger
///         kill earns the same reward set as a normal-hit kill on all of them. <paramref name="isStunTrigger" />
///         is accepted only to keep this method a drop-in signature match for
///         <see cref="PvpKillRewardZoneCatalog.Resolve" />; it is intentionally never read here.
///     </para>
///     <para>
///         The regular-war-drop zone family below is the drop-eligibility list (26 maps,
///         <c>S07_MyGame03.cpp:2950-2986</c>) -- a strictly BROADER set than the 11-map "regular-war server set"
///         (<c>RegularWarMapCatalog</c>) that drives the flat +20 CP override and the times-150 EXP multiplier.
///         A map can enable regular-war-drop eligibility here yet not be one of the 11 flat-override server
///         numbers; the two must stay distinct.
///     </para>
///     <para>
///         Symbol-rank-point eligibility (the symbol-battle zones' sixth channel) is not modeled: its legacy
///         grant sits behind the commented-out <c>USE_RANK_POINT</c> macro (<c>DEFINE.h:77</c>), dead in every
///         shipped build -- the same reason <see cref="PvpKillRewardZoneCatalog" /> omits it.
///     </para>
/// </remarks>
public static class PvpKillExtendedRewardZones
{
    /// <summary>Map 38, "DTM" (<c>S07_MyGame03.cpp:2887-2948</c>): always force-enables XP and CP.</summary>
    public const short DtmZoneId = 38;

    /// <summary>
    ///     The tribe-symbol-battle secondary zones (<c>S07_MyGame03.cpp:2853-2886</c>): grant the full
    ///     CP/EXP/drop/daily-mission set only while that zone's tribe-symbol-battle world flag is active; grant
    ///     nothing at all while it is inactive (the alternate branch is dead <c>#ifndef LNW33</c> code).
    /// </summary>
    private static readonly FrozenSet<short> SymbolBattleZoneIds = new short[]
    {
        2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143
    }.ToFrozenSet();

    /// <summary>
    ///     The regular-war-drop zone family (<c>S07_MyGame03.cpp:2950-2986</c>): full CP/EXP/drop/daily-mission
    ///     set, ungated and with no stun filter. Broader than the 11-map regular-war server set -- see class
    ///     remarks.
    /// </summary>
    private static readonly FrozenSet<short> RegularWarDropZoneIds = new short[]
    {
        49, 146, 149, 154, 157, 160, 120, 121, 122,
        51, 147, 150, 155, 158, 161,
        53, 148, 151, 152, 153, 156, 159, 162, 163, 164,
        295, 296
    }.ToFrozenSet();

    /// <summary>
    ///     Maps 195/196 (<c>S07_MyGame03.cpp:3017-3029</c>, live only under the always-defined <c>LNW33</c>):
    ///     full set, no stun filter, only while the map-195 time-window event flag is open; nothing while closed.
    /// </summary>
    private static readonly FrozenSet<short> Map195EventZoneIds = new short[] { 195, 196 }.ToFrozenSet();

    private static readonly PvpKillZoneRewardProfile FullReward = new(true, true, true, true, 0);

    /// <summary>
    ///     Resolves the reward-eligibility profile for the four extended zone families, or <c>null</c> when
    ///     <paramref name="zoneId" /> belongs to none of them (the caller then falls through to
    ///     <see cref="PvpKillRewardZoneCatalog.Resolve" />). <paramref name="runtime" /> supplies the live
    ///     world/event toggles the symbol-battle, map-38, and 195/196 branches gate on.
    /// </summary>
    public static PvpKillZoneRewardProfile? TryResolve(short zoneId, bool isStunTrigger,
        PvpKillRewardZoneRuntimeState runtime)
    {
        if (SymbolBattleZoneIds.Contains(zoneId))
            return runtime.SymbolBattleActive ? FullReward : PvpKillZoneRewardProfile.None;

        if (zoneId == DtmZoneId)
            // XP and CP are force-enabled regardless of which branch the map-38 handling takes; only drop and
            // daily-mission progress are gated on the runtime battle-post-distance / battle-state branch this
            // contract cannot statically recover (see PvpKillRewardZoneRuntimeState). Stun is not filtered.
            return new PvpKillZoneRewardProfile(true, true, runtime.Map38DropEnabled,
                runtime.Map38DailyMissionEnabled, 0);

        if (RegularWarDropZoneIds.Contains(zoneId))
            return FullReward;

        if (Map195EventZoneIds.Contains(zoneId))
            return runtime.Map195TimeEventActive ? FullReward : PvpKillZoneRewardProfile.None;

        return null;
    }
}
