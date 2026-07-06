using Microsoft.Extensions.Logging;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The side effect Zone039 performs once a sweep is due: forcibly relocate <paramref name="player" /> (who
///     no longer matches the Holy Stone's holder tribe) out of the territory map. No implementation is wired
///     by default -- see <see cref="HolyStoneTerritoryEvictionSweep" />'s own remarks for why the actual
///     destination is a documented gap rather than a guess.
/// </summary>
public interface IHolyStoneForcedReturnGateway
{
    void ForceReturnToSafeLocation(Zone zone, PlayerRuntimeState player);
}

/// <summary>
///     Logs the eviction instead of moving anyone -- the safe placeholder until the real "default/safe
///     location" destination is known (see <see cref="HolyStoneTerritoryEvictionSweep" />'s remarks). Wiring
///     this in production instead of a real gateway means Zone039's eviction decision runs correctly (the
///     right characters are identified every cycle, and the decision is logged) but no player actually moves
///     yet.
/// </summary>
public sealed class LoggingOnlyHolyStoneForcedReturnGateway(ILogger<LoggingOnlyHolyStoneForcedReturnGateway> logger)
    : IHolyStoneForcedReturnGateway
{
    public void ForceReturnToSafeLocation(Zone zone, PlayerRuntimeState player)
    {
        logger.LogWarning(
            "HolyStoneTerritory: character {CharacterId} on zone {MapId} should be forcibly returned to its " +
            "default/safe location (no longer matches the Stone's holder tribe), but no real forced-return " +
            "destination is wired yet -- see HolyStoneTerritoryEvictionSweep's remarks", player.CharacterId,
            zone.MapId);
    }
}

/// <summary>
///     Zone039's repeating, unconditional eviction sweep over the Holy Stone's territory maps: idle ~1
///     simulated minute, then ~3 more simulated seconds, then evict every fully-loaded, non-mid-transfer
///     character whose tribe (counting a declared ally) no longer matches
///     <see cref="WorldStateService.World" />'s <see cref="WorldRvrState.Zone038WinTribe" /> -- independent of,
///     and a backstop for, the separate zone-transfer entry gate that is supposed to keep them out in the
///     first place. "Fully loaded, non-mid-transfer" needs no separate flag check here: a character only ever
///     appears in <see cref="Zone.Players" /> once fully entered (the same fact
///     <c>Zone.CanAttackTowerGuardian</c>'s own remarks rely on), so enumerating it already excludes anyone
///     mid-handoff.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:4289-4342 (full eviction-sweep state machine) ; :821-834
///     (boot-time gate listing the qualifying server numbers -- translated the same way
///     <c>GameServerOptions.Zone241DungeonMapIds</c> already translates its own numbered-server-instance gate:
///     Fenrir shards by map, not by a numbered server-instance-per-process, so <see cref="_territoryMapIds" />
///     is an operator-configured map-id set, and a shard that hosts none of them makes every sweep on it a
///     no-op) ; :2895-2909 (per-tick invocation gate) ;
///     Server/ts25zone/S05_MyTransfer.cpp:1166-1179 (the forced-return call's payload -- confirmed to carry no
///     extra data beyond the bare notice, so <see cref="IHolyStoneForcedReturnGateway" /> needs no payload
///     parameter either) ; Server/Header/S19_MyZoneMoveInfo.cpp:1015-1038 (the separate, unconditional
///     zone-transfer entry check this sweep backstops -- not implemented by this class) ;
///     Server/ts25zone/S07_MyGame01.cpp:3114-3117 (the tribe-or-declared-ally match helper, see
///     <see cref="HolyStoneTribeMatch" />).
///     <para>
///         GAP -- not modeled, deliberately not guessed: (1) the exact "fixed, small set" of territory map ids
///         is not named by the translated contract, so <see cref="_territoryMapIds" /> must be supplied by the
///         caller/configuration rather than hardcoded; (2) the real "default/safe location" a forced return
///         relocates to is not given either -- see <see cref="IHolyStoneForcedReturnGateway" />'s remarks; (3)
///         the companion ~10-simulated-second boss/monster/tribe-symbol spawn-maintenance step co-located in
///         the same per-tick gated block is a distinct mechanic (monster spawning), out of scope here.
///     </para>
/// </remarks>
public sealed class HolyStoneTerritoryEvictionSweep(
    WorldStateService worldState,
    ZoneRegistry zones,
    IReadOnlyCollection<short> territoryMapIds,
    IHolyStoneForcedReturnGateway forcedReturn,
    ILogger<HolyStoneTerritoryEvictionSweep> logger)
{
    /// <summary>The idle half of the ~63 s cycle: roughly one simulated minute.</summary>
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(1);

    /// <summary>The grace half of the ~63 s cycle: roughly three simulated seconds.</summary>
    public static readonly TimeSpan GraceInterval = TimeSpan.FromSeconds(3);

    private readonly IReadOnlyCollection<short> _territoryMapIds = territoryMapIds;
    private TimeSpan _accumulated;

    public HolyStoneTerritoryEvictionPhase Phase { get; private set; } = HolyStoneTerritoryEvictionPhase.Idle;

    /// <summary>Advances the sweep by real time <paramref name="elapsed" />; catches up correctly on a stalled host.</summary>
    public void Tick(TimeSpan elapsed)
    {
        _accumulated += elapsed;

        while (true)
        {
            var threshold = Phase == HolyStoneTerritoryEvictionPhase.Idle ? IdleInterval : GraceInterval;
            if (_accumulated < threshold)
                return;

            _accumulated -= threshold;

            if (Phase == HolyStoneTerritoryEvictionPhase.Idle)
            {
                Phase = HolyStoneTerritoryEvictionPhase.Grace;
            }
            else
            {
                RunSweep();
                Phase = HolyStoneTerritoryEvictionPhase.Idle;
            }
        }
    }

    private void RunSweep()
    {
        var holderTribe = worldState.World.Zone038WinTribe;
        var allyOfHolder = holderTribe is { } holder ? worldState.GetAllyOf(holder) : null;

        foreach (var mapId in _territoryMapIds)
        {
            if (!zones.TryGet(mapId, out var zone) || zone is null)
                continue;

            List<PlayerRuntimeState>? toEvict = null;
            foreach (var player in zone.Players)
                if (!HolyStoneTribeMatch.Matches(player.Tribe, holderTribe, allyOfHolder))
                    (toEvict ??= []).Add(player);

            if (toEvict is null)
                continue;

            foreach (var player in toEvict)
            {
                logger.LogInformation(
                    "HolyStoneTerritory: evicting character {CharacterId} (tribe {Tribe}) from zone {MapId} -- no longer matches holder tribe {HolderTribe}",
                    player.CharacterId, player.Tribe, mapId, holderTribe);
                forcedReturn.ForceReturnToSafeLocation(zone, player);
            }
        }
    }
}

/// <summary>Which half of <see cref="HolyStoneTerritoryEvictionSweep" />'s ~63 s cycle is currently in progress.</summary>
public enum HolyStoneTerritoryEvictionPhase : byte
{
    /// <summary>The ~1-minute idle wait.</summary>
    Idle,

    /// <summary>The further ~3-second grace wait immediately before the sweep itself runs.</summary>
    Grace
}
