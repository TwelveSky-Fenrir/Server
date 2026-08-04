using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class Zone175LabyrinthSystem(
    Zone175LabyrinthConfig config,
    ZoneCenterSiegeState siegeState,
    MonsterSpawnScheduler monsterSpawnScheduler,
    Lazy<ZoneCenterBroadcastIngestor> centerBroadcastIngestor,
    ILogger<Zone175LabyrinthSystem> logger,
    TimeProvider? timeProvider = null) : ISimulationSystem
{
    private readonly ConcurrentDictionary<short, Zone175ZoneRuntime> _runtimeByZone = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (!config.TryGet(zone.MapId, out var instanceConfig))
            return;

        if (!_runtimeByZone.TryGetValue(zone.MapId, out var runtime))
            runtime = _runtimeByZone.GetOrAdd(zone.MapId,
                new Zone175ZoneRuntime(new Zone175MissionState(),
                    new ZoneZone175MissionEffects(zone, instanceConfig, monsterSpawnScheduler, centerBroadcastIngestor,
                        logger)));

        Zone175MissionCore.Advance(runtime.State, in instanceConfig, runtime.Effects,
            _timeProvider.GetLocalNow(), siegeState.GetZone175(instanceConfig.Index1, instanceConfig.Index2),
            legacyTicksElapsed);
    }

    public bool IsZone175Map(short mapId)
    {
        return config.TryGet(mapId, out _);
    }

    public bool TryGetPhase(short mapId, out Zone175MissionPhase phase)
    {
        if (_runtimeByZone.TryGetValue(mapId, out var runtime))
        {
            phase = runtime.State.Phase;
            return true;
        }

        phase = Zone175MissionPhase.Idle;
        return false;
    }

    private sealed class Zone175ZoneRuntime(Zone175MissionState state, IZone175MissionEffects effects)
    {
        public Zone175MissionState State { get; } = state;
        public IZone175MissionEffects Effects { get; } = effects;
    }
}
