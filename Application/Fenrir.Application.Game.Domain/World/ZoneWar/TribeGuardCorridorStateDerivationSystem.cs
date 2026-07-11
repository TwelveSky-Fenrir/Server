using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

internal sealed class CorridorZoneState
{
    public bool BootPassDone;
}

public sealed class TribeGuardCorridorStateDerivationSystem(
    TribeGuardCorridorCatalog catalog,
    TribeGuardCorridorState state) : ISimulationSystem
{

        public const int GuardPostsPerSegment = 5;

    private readonly ConcurrentDictionary<short, CorridorZoneState> _stateByZone = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var owned = catalog.GetSegmentsOwnedByZone(zone.MapId);
        if (owned.Count == 0)
            return;

        var zoneState = _stateByZone.GetOrAdd(zone.MapId, static _ => new CorridorZoneState());

        if (!zoneState.BootPassDone)
        {
            zoneState.BootPassDone = true;
            foreach (var (tribeId, segmentIndex) in owned)
                state.TrySetOpen(tribeId, segmentIndex, true);

            return;
        }

        foreach (var (tribeId, segmentIndex) in owned)
            EvaluateSegment(zone, tribeId, segmentIndex);
    }

    private void EvaluateSegment(Zone zone, byte tribeId, byte segmentIndex)
    {
        if (!catalog.TryGetGuardPostSlots(tribeId, segmentIndex, out var slots))
            return;

        var anyAlive = false;
        foreach (var slot in slots)
            if (zone.TryGetMonster(slot, out _))
            {
                anyAlive = true;
                break;
            }

        var currentlyOpen = state.IsOpen(tribeId, segmentIndex);
        if (!anyAlive && !currentlyOpen)
            state.TrySetOpen(tribeId, segmentIndex, true);
        else if (anyAlive && currentlyOpen)
            state.TrySetOpen(tribeId, segmentIndex, false);
    }
}
