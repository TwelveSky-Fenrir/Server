using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     <c>USE_REGULAR_WAR_AFK_TICK</c>: forces an avatar out of a Regular War (Zone049) instance mid-active-battle,
///     or any Zone195 "Nok-San" instance at all, once it has continuously accrued enough legacy ticks on that
///     map -- first a repeated warning, then a forced return-to-auto-zone notice, then (a couple ticks later,
///     if it is still present) a hard disconnect.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:2017-2072 (the per-avatar <c>mAFKTick</c> accrual, the
///     60-tick warning cadence, the threshold*60 forced-return, and the threshold*60+2 hard disconnect --
///     thresholds 5 for the Zone049-active-battle gate, 10 for any Zone195 instance) ;
///     Server/ts25zone/H01_MainApplication.h:20-22 (<c>USE_REGULAR_WAR_AFK_TICK</c> unconditionally defined
///     under <c>LNW33</c>) ; Server/BuildEU33/ServerInfo.ini:143 (<c>TimeLogic=500</c>, the legacy-tick
///     duration this class's thresholds are expressed in).
///     <para>
///         GAP -- not modeled, deliberately not guessed: (1) the exact wire sort/opcode for the "Start X/Y"
///         interim warning message was not cited by the translated behavior contract this class implements
///         (it only describes the message as "a system/secret chat message"), so the warning is logged only
///         here -- a real client-facing broadcast needs a fresh legacy-behavior-translator/wire-protocol
///         finding for the exact opcode before it can be wired in, same posture as
///         <see cref="HolyStoneWarCycle" />'s several uncited interim notices; (2) whether <c>mAFKTick</c>
///         resets, decrements, or persists once the enforcement condition stops holding for a tick was flagged
///         by the translated contract as an open question -- this class resets the counter to zero the
///         instant its own map stops enforcing the mechanic (matching the sibling
///         <see cref="PlayerRuntimeState.AntiCampingProximityCounter" />'s own "not in range -> reset"
///         convention elsewhere in this same cluster), which is the safest default (it can never let a stale
///         accumulation from a PREVIOUS war/instance window fast-forward a player toward eviction in a fresh
///         one) but is an explicit engineering choice, not a re-derived legacy fact -- flag for
///         <c>cpp-ts25-explorer</c> re-check if the exact reset semantics matter for byte-for-byte parity.
///     </para>
///     <para>
///         The two dead sibling gates the translated contract also names (Zone194/Zone267, both never armed in
///         the shipped build) are not modeled here at all -- there is no map-id catalog for either anywhere in
///         this codebase, and arming this system on a map id that can never be a real Zone194/267 instance
///         would be pure dead branches, not a faithful reproduction of anything reachable.
///     </para>
/// </remarks>
public sealed class RegularWarAfkTickSystem(
    RegularWarActiveMapTracker regularWarTracker,
    IOptions<GameServerOptions> options,
    ILogger<RegularWarAfkTickSystem> logger) : ISimulationSystem
{
    /// <summary>Zone195 "at all" full-AFK threshold, in 60-legacy-tick units (10 units = 600 ticks = 5 real minutes).</summary>
    public const int Zone195FullUnits = 10;

    /// <summary>Zone049-active-battle full-AFK threshold, in 60-legacy-tick units (5 units = 300 ticks = 2.5 real minutes).</summary>
    public const int WarActiveFullUnits = 5;

    /// <summary>The warning cadence and the unit size the two thresholds above are expressed in.</summary>
    public const int UnitLegacyTicks = 60;

    /// <summary>How many further legacy ticks past the full threshold trigger the hard disconnect.</summary>
    public const int DisconnectGraceLegacyTicksPastFull = 2;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var opts = options.Value;
        var isZone195 = opts.Zone195MapIds.Contains(zone.MapId);

        // Cheapest possible no-op for the overwhelming majority of zones (every ordinary town/field map is
        // neither a configured Zone195 instance nor even a Regular War map at all) -- avoids paying for a full
        // player scan on every zone, every tick, just to confirm there is nothing to reset. Only maps that
        // could ever have accrued a nonzero AfkTick in the first place fall through to the per-player work
        // below.
        if (!isZone195 && !RegularWarMapCatalog.TryGet(zone.MapId, out _))
            return;

        var isWarActive = !isZone195 && regularWarTracker.IsBattleInProgress(zone.MapId);

        if (!isZone195 && !isWarActive)
        {
            ResetEveryPlayer(zone);
            return;
        }

        var fullUnits = isZone195 ? Zone195FullUnits : WarActiveFullUnits;
        var fullTicks = fullUnits * UnitLegacyTicks;

        List<PlayerRuntimeState>? toAutoReturn = null;
        List<PlayerRuntimeState>? toDisconnect = null;

        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone)
                continue;

            var previousTicks = player.AfkTick;
            player.AfkTick += legacyTicksElapsed;

            var previousUnit = previousTicks / UnitLegacyTicks;
            var currentUnit = player.AfkTick / UnitLegacyTicks;
            if (currentUnit > previousUnit && player.AfkTick < fullTicks)
                // GAP: no cited wire sort for this interim warning -- logged only, see class remarks.
                logger.LogInformation(
                    "RegularWarAfkTick: character {CharacterId} on zone {MapId} idle warning {Current}/{Full}",
                    player.CharacterId, zone.MapId, currentUnit, fullUnits);

            if (previousTicks < fullTicks && player.AfkTick >= fullTicks)
                (toAutoReturn ??= []).Add(player);

            if (player.AfkTick >= fullTicks + DisconnectGraceLegacyTicksPastFull)
                (toDisconnect ??= []).Add(player);
        }

        if (toAutoReturn is not null)
            foreach (var player in toAutoReturn)
                player.Session.Send(new ReturnToHomeZoneResponse());

        if (toDisconnect is null)
            return;

        // Deferred to after the full player scan, same posture as DeathGateTickSystem/HoisundoCountdownSystem's
        // own toForceQuit/toDisconnect lists: Abort() only requests teardown, so disconnecting mid-enumeration
        // here would risk mutating the very collection this loop is iterating.
        foreach (var player in toDisconnect)
            if (player.Session is ClientSession client)
                client.Abort(DisconnectReason.IdleTimeout);
    }

    private static void ResetEveryPlayer(Zone zone)
    {
        foreach (var player in zone.Players)
            if (player.AfkTick != 0)
                player.AfkTick = 0;
    }
}
