using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     FIX_HSB_POS_BUG: the anti-camping forced-return-to-auto-zone check for Holy Stone symbol locations
///     and Tower positions. Runs once per legacy tick for every occupied avatar slot on a guarded map
///     (<see cref="AntiCampingGuardPointCatalog.GuardedMapIds" />), tracking how many consecutive qualifying
///     ticks each avatar has stood within <see cref="ProximityRadius" /> of a guarded point; once
///     <see cref="ForcedReturnThreshold" /> is reached, the avatar is repeatedly told to return to its auto
///     zone for as long as it stays in range, with no cooldown and no escalation to disconnection.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1739 (<c>MyGame::Logic</c> -- confirms this lives inside
///     the periodic zone tick, not a packet handler) ; :2024-2029 (per-tick working state, including the
///     per-tick "currently flagged" indicator modeled here as the local <c>flaggedThisTick</c>, declared once
///     per tick outside the per-avatar loop) ; :2031-2038 (outer per-avatar loop -- only ready avatar slots
///     are processed ; this check sits before the position-broadcast throttle (:2432), hide-state handling
///     (:2437), and death-state handling (:2447) later in the same loop body) ; :2074-2371 (guarded-map
///     switch -- see <see cref="AntiCampingGuardPointCatalog" /> for the full citation of the coordinate
///     table's shape and its own documented data gap) ; :2373-2406 (Tower proximity addendum, evaluated only
///     when no Holy Stone symbol check already flagged the avatar this tick, then the forced-return call once
///     the counter reaches 20) ; Server/ts25zone/H07_MyGame.h:57-59 (<c>FIX_HSB_POS_BUG</c> unconditionally
///     defined under <c>LNW33</c>, always active in the shipped <c>ReleaseEU33</c> build -- live production
///     behavior) ; Server/Header/Protocol/DEFINE.h:86 (<c>USE_TOWER</c> unconditionally defined -- the Tower
///     branch is also live) ; Server/ts25zone/H07_MyGame.h:1039 and Server/ts25zone/S07_MyGame04.cpp:124-126
///     (the per-avatar counter itself, <see cref="PlayerRuntimeState.AntiCampingProximityCounter" />) ;
///     Server/Header/mapcheck.h:12-15 (<c>GetLengthXYZ</c> -- full three-axis distance) ;
///     Server/ts25zone/H05_MyTransfer.h:116-118 and Server/ts25zone/S05_MyTransfer.cpp:1166-1179
///     (<c>MyTransfer::B_RETURN_TO_AUTO_ZONE</c> -- the outbound notification carries no fields beyond the
///     packet header, matching <see cref="ReturnToHomeZoneResponse" />'s own empty payload) ;
///     Server/Header/Protocol/ZONE.h:942-944,1587-1588 (<c>ZC_RETURN_TO_AUTO_ZONE</c>/
///     <c>ZCP_RETURN_TO_AUTO_ZONE</c> -- the header-only wire shape).
///     <para>
///         Registered first among every <see cref="ISimulationSystem" /> (see
///         <c>Fenrir.Application.Game.Domain.Extensions.DomainServiceCollectionExtensions</c>) for the same
///         tick-order-fidelity reason <c>DeathGateTickSystem</c> is registered last: the legacy check runs
///         before any other per-avatar tick work in the same pass.
///     </para>
///     <para>
///         GAP -- not modeled, deliberately not guessed: (1) the real per-map coordinate data -- see
///         <see cref="AntiCampingGuardPointCatalog" />'s own remarks; (2) the separate, unrelated AFK-kick
///         mechanic whose own forced-return/disconnect decision (if it already fired earlier in the same
///         tick's per-avatar pass) is supposed to skip this check entirely for that avatar -- no such
///         mechanic exists yet anywhere in Fenrir, so nothing skips this check today; whoever implements it
///         must register their own <see cref="ISimulationSystem" /> ahead of this one and give this class a
///         way to observe "already handled this tick" for a given avatar; (3) once the forced-return
///         notification is sent, the legacy skips the remainder of that avatar's own tick processing
///         (position-broadcast throttle, hide-state, death-state) for that tick only -- reproducing that
///         precisely would require every other simulation system and <c>Zone.PlayerLifecycle.RebroadcastAvatars</c>
///         to observe the same "already handled" signal, which spans well beyond this cluster's World/RvR
///         scope; the notification itself (the behaviorally significant, observable part of this mechanism)
///         is still sent correctly and repeatedly every tick regardless.
///     </para>
/// </remarks>
public sealed class AntiCampingForcedReturnSystem(AntiCampingGuardPointCatalog catalog) : ISimulationSystem
{
    /// <summary>The fixed proximity radius every guarded-point check compares against (S07_MyGame01.cpp:2074-2371).</summary>
    public const float ProximityRadius = 40.0f;

    /// <summary>
    ///     The fixed consecutive-tick threshold that triggers the forced-return notification
    ///     (S07_MyGame01.cpp:2373-2406).
    /// </summary>
    public const int ForcedReturnThreshold = 20;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (!catalog.IsGuardedMap(zone.MapId))
            return;

        var points = catalog.GetPoints(zone.MapId);
        if (points.HolyStoneSymbolPoints.IsEmpty && points.TowerPoint is null)
            return;

        foreach (var player in zone.Players)
            EvaluatePlayer(player, points, legacyTicksElapsed);
    }

    private static void EvaluatePlayer(PlayerRuntimeState player, AntiCampingMapGuardPoints points,
        int legacyTicksElapsed)
    {
        // Every Holy Stone symbol point is evaluated in order, unconditionally -- each point's "not in range"
        // outcome resets the counter, so only the last-evaluated point's outcome survives to the end of this
        // avatar's tick (a real, faithfully-reproduced quirk of the legacy ordering, not an "any of N" gate).
        // The "currently flagged" indicator, unlike the counter, is only ever set true here -- never unset --
        // once any point flags it this tick.
        var flaggedThisTick = false;

        foreach (var symbolPoint in points.HolyStoneSymbolPoints)
            flaggedThisTick |= EvaluatePoint(player, symbolPoint, legacyTicksElapsed);

        // The Tower check runs only when nothing above already flagged this avatar this tick.
        if (!flaggedThisTick && points.TowerPoint is { } towerPoint)
            EvaluatePoint(player, towerPoint, legacyTicksElapsed);

        // Reaching (or exceeding) the threshold neither resets the counter nor records that a notification
        // was already sent -- the same notification is re-sent every tick for as long as the avatar remains
        // in range, with no cooldown and no escalation to disconnection.
        if (player.AntiCampingProximityCounter >= ForcedReturnThreshold)
            player.Session.Send(new ReturnToHomeZoneResponse());
    }

    /// <summary>
    ///     Applies the increment-on-proximity / reset-on-distance rule for exactly one guarded point, and
    ///     returns whether this evaluation itself found the avatar in range.
    /// </summary>
    private static bool EvaluatePoint(PlayerRuntimeState player, AntiCampingGuardPoint point, int legacyTicksElapsed)
    {
        if (!IsWithinRadius(player, point))
        {
            player.AntiCampingProximityCounter = 0;
            return false;
        }

        // Incremented by the whole elapsed legacy-tick count (not a hardcoded +1) so a stalled-host catch-up
        // burst counts as that many consecutive qualifying ticks at the avatar's current (unchanged-during-
        // the-burst) position -- the same "advance multi-tick counters by the full amount" convention every
        // other burst-aware ISimulationSystem in this codebase already follows (see ISimulationSystem's own
        // doc remarks).
        player.AntiCampingProximityCounter += legacyTicksElapsed;
        return true;
    }

    private static bool IsWithinRadius(PlayerRuntimeState player, AntiCampingGuardPoint point)
    {
        var dx = player.PosX - point.X;
        var dy = player.PosY - point.Y;
        var dz = player.PosZ - point.Z;
        var distanceSquared = dx * dx + dy * dy + dz * dz;
        return distanceSquared <= ProximityRadius * ProximityRadius;
    }
}
