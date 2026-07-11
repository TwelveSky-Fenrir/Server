using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The side effect Zone039's five gated servers perform once case 38 (Zone038 win) reaches them, minus the
///     eviction sweep -- that half of the legacy reaction is a SEPARATE, independently ticked state machine
///     already fully implemented by <see cref="HolyStoneTerritoryEvictionSweep" /> (same source citations,
///     <c>Server/ts25zone/S07_MyGame01.cpp:4289-4342</c> full eviction state machine / <c>:821-834</c> the
///     boot-time gated-server list, which is the SAME five-server set as
///     <see cref="ScheduledZoneCenterEventCodes.Zone039ArmingGatedMapIds" /> -- do not re-implement eviction
///     here; wire that host as usual).
/// </summary>
/// <remarks>
///     Réf. C++ (via the A12 scheduled-events behavior contract) : Server/ts25zone/S07_MyGame08.cpp:202-218
///     (case 38's zone-side reaction: battle-state flag set, server-74-only monster-summon reset,
///     confirmed-dead Zone039 boss-summon call) ; Server/ts25zone/S10_MySummon.cpp:1056-1064
///     (<c>ResetMonsterSummonState</c>) ; Server/ts25zone/S07_MyGame01.cpp:4290-4300 (<c>SummonBossZone039</c>,
///     confirmed a guaranteed no-op at both call sites -- deliberately NOT reproduced here).
///     <para>
///         <see cref="Zone039ArmingState" />'s battle-state flag/tick-counter pair (legacy
///         <c>mZone039TypeBattleState</c>/<c>mZone039TypePostTick</c>) is kept purely for documented parity:
///         the contract's own point 5 states the actual observable "arm" payload (the eviction sweep) is
///         carried by a SEPARATE, independently ticked state machine that never reads this flag -- no
///         downstream reader of this flag exists anywhere in the traced source. It is stored here rather than
///         omitted only so a future finding that DOES identify a reader has somewhere correct to read it from.
///     </para>
/// </remarks>
public sealed class Zone039ArmingReactor(IZone039MonsterSummonResetGateway gateway)
{
    private readonly ConcurrentDictionary<short, Zone039ArmingState> _stateByZone = new();

    /// <summary>
    ///     Applies the case-38 zone-side reaction to every zone THIS shard hosts that is one of the five gated
    ///     map ids. Call once per Zone038-win decision (both the originating shard's own
    ///     <c>ZoneEventBroadcaster.AnnounceZone038Winner</c> and every OTHER shard's
    ///     <c>ZoneEventBroadcaster.ApplyRelayedEvent</c> case-38 replay -- see wiringManifest).
    /// </summary>
    public void Apply(ZoneRegistry zones)
    {
        foreach (var zone in zones.Zones)
        {
            if (!ScheduledZoneCenterEventCodes.Zone039ArmingGatedMapIds.Contains(zone.MapId))
                continue;

            var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new Zone039ArmingState());
            state.BattleState = 1;
            state.PostTick = 0;

            if (zone.MapId == ScheduledZoneCenterEventCodes.Zone039MonsterSummonResetMapId)
                gateway.ResetGeneralSpawnTable(zone);
        }
    }

    /// <summary>Test/diagnostic accessor -- returns the vestigial per-zone flag pair, if this zone was ever armed.</summary>
    public Zone039ArmingState? TryGetState(short mapId)
    {
        return _stateByZone.TryGetValue(mapId, out var state) ? state : null;
    }
}

/// <summary>
///     Legacy <c>mZone039TypeBattleState</c>/<c>mZone039TypePostTick</c> -- see <see cref="Zone039ArmingReactor" />'s
///     own remarks for why this has no known downstream reader today.
/// </summary>
public sealed class Zone039ArmingState
{
    public int BattleState;
    public int PostTick;
}

/// <summary>
///     The side effect Zone039's server-74 reaction performs: force-invalidate every currently valid slot of
///     the given zone's general (non-boss) monster spawn table for a fresh re-summon cycle
///     (<c>ResetMonsterSummonState</c>, <c>Server/ts25zone/S10_MySummon.cpp:1056-1064</c>). No implementation
///     is wired by default -- see <see cref="LoggingOnlyZone039MonsterSummonResetGateway" />'s own remarks for
///     why the real reset is a documented gap rather than a guess.
/// </summary>
public interface IZone039MonsterSummonResetGateway
{
    /// <param name="zone">The zone whose general monster spawn table should be force-invalidated.</param>
    public void ResetGeneralSpawnTable(Zone zone);
}

/// <summary>
///     Logs the reset instead of performing it -- the safe placeholder until <c>MonsterSpawnScheduler</c>
///     exposes a public "force every slot to respawn now" hook. <see cref="Monsters.MonsterSpawnScheduler" />'s
///     own per-zone spawn-slot state (<c>MonsterZoneSpawnState</c>/<c>MonsterSpawnSlot</c>) is <c>internal</c> and
///     that class is <c>sealed</c>, so no new file in this codebase can reach in and mutate it directly -- the
///     real gateway implementation needs a small new public method added to that existing file (see this
///     task's wiringManifest); wiring this placeholder in production instead means Zone039's server-74
///     reaction runs correctly (the right zone is identified every case-38 event, and the omission is logged)
///     but no monster slot is actually force-respawned yet.
/// </summary>
public sealed class LoggingOnlyZone039MonsterSummonResetGateway(
    ILogger<LoggingOnlyZone039MonsterSummonResetGateway> logger) : IZone039MonsterSummonResetGateway
{
    public void ResetGeneralSpawnTable(Zone zone)
    {
        logger.LogWarning(
            "Zone039Arming: zone {MapId} should force-invalidate its general monster spawn table for a fresh " +
            "re-summon cycle (legacy ResetMonsterSummonState), but no real MonsterSpawnScheduler reset hook is " +
            "wired yet -- see Zone039ArmingReactor's remarks / this task's wiringManifest", zone.MapId);
    }
}
