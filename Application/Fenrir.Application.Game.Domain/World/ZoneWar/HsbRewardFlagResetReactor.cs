namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Case 4000's zone-side reaction, run on EVERY zone shard with no server-number restriction (unlike
///     case 38's Zone039-arming reaction above): resets every currently connected ("ready") character's
///     one-time-per-HSB-window reward-eligibility flag back to eligible.
/// </summary>
/// <remarks>
///     Réf. C++ (via the A12 scheduled-events behavior contract) : Server/ts25zone/S07_MyGame08.cpp:1313-1319
///     (iterates every ready user, resets <c>bHSBStoneRewardCheck</c> to zero) ;
///     Server/ts25zone/S07_MyGame02.cpp:2686-2702
///     (the reward the flag gates -- killing a tribe-symbol-guard monster, item ids 1448/723/1073; that
///     GRANT/consume side is a combat-reward feature OUTSIDE this contract's scope and is deliberately not
///     implemented here, only the reset) ; Server/Header/CSQLAvatar.cpp:730 (the flag is DB-persisted in
///     legacy; Fenrir has no backing column yet, so <see cref="PlayerRuntimeState.HsbStoneRewardClaimed" /> is
///     in-memory-only and resets to its default on zone (re)entry -- same "not yet persisted" posture as
///     <see cref="PlayerRuntimeState.WarPoint" />, per the contract's own noted offline-player staleness gap).
///     <para>
///         "Ready" needs no separate flag check: a character only ever appears in <see cref="Zone.Players" />
///         once fully entered, the same fact <see cref="HolyStoneTerritoryEvictionSweep" />'s own remarks and
///         <c>Zone.CanAttackTowerGuardian</c> rely on.
///     </para>
/// </remarks>
public static class HsbRewardFlagResetReactor
{
    /// <summary>
    ///     Called for case 4000 (<see cref="ZoneCenterBroadcastIngestor.PingEventCode" />) -- see this task's
    ///     wiringManifest for the exact <c>ZoneCenterBroadcastIngestor.ApplyStateEffect</c> edit that invokes
    ///     this.
    /// </summary>
    public static void Apply(ZoneRegistry zones)
    {
        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            player.HsbStoneRewardClaimed = false;
    }
}
