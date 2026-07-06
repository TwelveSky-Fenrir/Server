using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide, thread-safe read side for "is Regular War (Zone049) currently in its capture/score window
///     on this map." <c>RegularWarSchedulerHost</c> (<c>Fenrir.Application.Game.Hosting.World.ZoneWar</c>) reports
///     each hosted map's <see cref="RegularWarSchedule.Phase" /> here once per tick;
///     <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s PvP kill-reward pipeline reads it back through
///     <see cref="IsBattleInProgress" /> to gate the Regular-War-host flat CP/War Point/Blood Point kill-reward
///     override (<see cref="Fenrir.Application.Game.Domain.Combat.PvpKillContributionPointCalculator" />).
/// </summary>
/// <remarks>
///     <para>
///         Same "Domain-owned shared singleton, Hosting writes it, Zone reads it" split already used by
///         <see cref="Fenrir.Application.Game.Domain.Progression.TowerWarState" /> and
///         <see cref="Fenrir.Application.Game.Domain.World.WorldState.WorldStateService" /> --
///         <c>Fenrir.Application.Game.Domain</c> itself never depends on
///         <c>Fenrir.Application.Game.Hosting</c>, so a small Domain-owned class is the one thing both sides can
///         share without inverting that dependency direction.
///     </para>
///     <para>
///         Réf. C++ : mirrors <c>mGAME.mCheckZone049TypeServer</c> (the host-enabled flag) combined with the
///         tracked zone's own state == 3 ("battle in progress") check, Server/ts25zone/S07_MyGame03.cpp:2404-2432.
///         Fenrir has no separate "Regular War globally enabled" toggle distinct from
///         <see cref="RegularWarMapCatalog.ConfiguredMaps" /> -- the fixed 11-map catalog IS the host-enabled
///         flag here, same collapse <see cref="RegularWarMapCatalog" />'s own remarks already describe for the
///         legacy's per-server one-time assignment.
///     </para>
/// </remarks>
public sealed class RegularWarActiveMapTracker
{
    private readonly ConcurrentDictionary<short, RegularWarPhase> _phaseByMapId = new();

    /// <summary>Overwrites <paramref name="mapId" />'s last-reported phase -- called once per tick, per hosted map.</summary>
    public void ReportPhase(short mapId, RegularWarPhase phase)
    {
        _phaseByMapId[mapId] = phase;
    }

    /// <summary>
    ///     True only when <paramref name="mapId" /> both runs the Regular War schedule at all
    ///     (<see cref="RegularWarMapCatalog.TryGet" />) and its last-reported phase is
    ///     <see cref="RegularWarPhase.Active" /> ("battle in progress", state 3) -- a map that is host-enabled but
    ///     currently mid-cooldown/open-gate/pre-war/post-war-cleanup/forced-reset does not qualify, matching the
    ///     source contract's "flagged as host-enabled but the tracked zone state is not this specific value"
    ///     edge case. A map this process has never reported a phase for yet (nothing hosted, or no tick has run)
    ///     is treated the same as "not active."
    /// </summary>
    public bool IsBattleInProgress(short mapId)
    {
        return RegularWarMapCatalog.TryGet(mapId, out _) &&
               _phaseByMapId.TryGetValue(mapId, out var phase) &&
               phase == RegularWarPhase.Active;
    }
}
