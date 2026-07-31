using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class Zone175LabyrinthSystem(
    Zone175LabyrinthConfig config,
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
                    new ZoneZone175MissionEffects(zone, instanceConfig, logger)));

        Zone175MissionCore.Advance(runtime.State, in instanceConfig, runtime.Effects,
            _timeProvider.GetUtcNow(), legacyTicksElapsed);
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
