using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The per-zone <see cref="ISimulationSystem" /> that runs the Zone175 "Labyrinth" 5-wave PvE mission. On
///     every legacy tick of a Zone175-type zone it self-schedules (arms the Sunday-21:00 open window itself,
///     from the injected <see cref="TimeProvider" />) and advances that zone's mission state machine
///     (<see cref="Zone175MissionCore" />) through the live-<see cref="Zone" />-backed effects adapter.
/// </summary>
/// <remarks>
///     Self-contained, like every other autonomous RvR/event state machine in the pipeline
///     (<c>Zone335FfaEventCycleSystem</c>, <c>ValleyWarSystem</c>): it reads only the passed <see cref="Zone" />,
///     the wall clock, and its own per-zone state; it adds no zone command channel and does not touch the
///     command drain, so its position among the other systems does not matter. A single DI singleton shared
///     across every zone this process ticks, so the actual per-zone mutable state and the per-zone effects
///     adapter live in <see cref="_runtimeByZone" />, built lazily on that zone's own first configured
///     <see cref="Simulate" /> call (never racy: each zone's tick is its own single thread).
///     <para>
///         Gated entirely by <see cref="Zone175LabyrinthConfig" />: a map absent from the catalog is skipped
///         immediately (no per-zone state is even created), so this is a zero-cost no-op on every non-Labyrinth
///         map. With the default <see cref="Zone175LabyrinthConfig.Disabled" /> catalog it is a no-op on every
///         map until Hosting supplies a populated catalog (see the workstream report's wiring note).
///     </para>
/// </remarks>
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
            return; // not a Zone175-type map -- nothing to build, nothing to run.

        // TryGetValue-then-GetOrAdd (rather than GetOrAdd with a factory lambda) so the closure/adapter is only
        // constructed on this zone's very first configured tick, never allocated per tick thereafter.
        if (!_runtimeByZone.TryGetValue(zone.MapId, out var runtime))
            runtime = _runtimeByZone.GetOrAdd(zone.MapId,
                new Zone175ZoneRuntime(new Zone175MissionState(),
                    new ZoneZone175MissionEffects(zone, instanceConfig, logger)));

        Zone175MissionCore.Advance(runtime.State, in instanceConfig, runtime.Effects,
            _timeProvider.GetUtcNow(), legacyTicksElapsed);
    }

    /// <summary>Test/diagnostic hook: the current mission phase for a map, if this zone is configured and has ticked.</summary>
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

    /// <summary>Per-zone mission state plus its reused effects adapter -- built once per zone.</summary>
    private sealed class Zone175ZoneRuntime(Zone175MissionState state, IZone175MissionEffects effects)
    {
        public Zone175MissionState State { get; } = state;
        public IZone175MissionEffects Effects { get; } = effects;
    }
}
