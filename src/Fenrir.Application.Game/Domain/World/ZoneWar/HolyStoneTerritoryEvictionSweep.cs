using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public interface IHolyStoneForcedReturnGateway
{
    public void ForceReturnToSafeLocation(Zone zone, PlayerRuntimeState player);
}

public sealed class HolyStoneForcedReturnGateway : IHolyStoneForcedReturnGateway
{
    public void ForceReturnToSafeLocation(Zone zone, PlayerRuntimeState player)
    {
        zone.PostHolyStoneForcedReturn(player.CharacterId);
    }
}

public sealed class HolyStoneTerritoryEvictionSweep(
    WorldStateService worldState,
    ZoneRegistry zones,
    IReadOnlyCollection<short> territoryMapIds,
    IHolyStoneForcedReturnGateway forcedReturn,
    ILogger<HolyStoneTerritoryEvictionSweep> logger)
{
    public const int IdleLegacyTicks = 120;

    public const int GraceLegacyTicks = 6;

    private readonly IReadOnlyCollection<short> _territoryMapIds = territoryMapIds;
    private int _accumulatedLegacyTicks;

    public HolyStoneTerritoryEvictionPhase Phase { get; private set; } = HolyStoneTerritoryEvictionPhase.Idle;

    public void Tick(int legacyTicksElapsed)
    {
        _accumulatedLegacyTicks += legacyTicksElapsed;

        while (true)
        {
            var threshold = Phase == HolyStoneTerritoryEvictionPhase.Idle ? IdleLegacyTicks : GraceLegacyTicks;
            if (_accumulatedLegacyTicks < threshold)
                return;

            _accumulatedLegacyTicks -= threshold;

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
