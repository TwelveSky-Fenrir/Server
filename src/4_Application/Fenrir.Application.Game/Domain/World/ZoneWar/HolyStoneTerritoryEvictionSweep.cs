using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public interface IHolyStoneForcedReturnGateway
{
    public void ForceReturnToSafeLocation(Zone zone, PlayerRuntimeState player);
}

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

public sealed class HolyStoneTerritoryEvictionSweep(
    WorldStateService worldState,
    ZoneRegistry zones,
    IReadOnlyCollection<short> territoryMapIds,
    IHolyStoneForcedReturnGateway forcedReturn,
    ILogger<HolyStoneTerritoryEvictionSweep> logger)
{
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan GraceInterval = TimeSpan.FromSeconds(3);

    private readonly IReadOnlyCollection<short> _territoryMapIds = territoryMapIds;
    private TimeSpan _accumulated;

    public HolyStoneTerritoryEvictionPhase Phase { get; private set; } = HolyStoneTerritoryEvictionPhase.Idle;

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

public enum HolyStoneTerritoryEvictionPhase : byte
{
    Idle,

    Grace
}
