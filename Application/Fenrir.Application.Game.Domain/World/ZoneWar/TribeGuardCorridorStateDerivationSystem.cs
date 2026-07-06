using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>Per-zone bookkeeping this system needs, tick-thread-owned -- just the one-shot boot-override flag.</summary>
internal sealed class CorridorZoneState
{
    public bool BootPassDone;
}

/// <summary>
///     <c>MyGame::ProcessForGuardState</c> (<c>Server/ts25zone/S07_MyGame01.cpp:3400-3526</c>): re-derives the
///     <see cref="TribeGuardCorridorState" /> table from live guard-post creature counts. Registered as an
///     <see cref="ISimulationSystem" /> like every sibling in this folder, but unlike
///     <see cref="TribeGuardSpawner" />/<see cref="TribeSymbolSpawner" /> it runs on EVERY tick, with no 20-tick
///     cadence gate (<c>Server/ts25zone/S07_MyGame01.cpp:2662</c>, called unconditionally from the main
///     simulation loop, no rate-limit guard around it).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3400-3526 (<c>ProcessForGuardState</c> -- per-zone dispatch
///     by own server/zone identity, no-op default for non-corridor/non-hub zones at :3490-3491, five-guard-post
///     liveness count per checkpoint, flip-and-notify-center logic at :3509-3524, hub zone re-deriving all four
///     tribes' segment-0 checkpoints in one pass at :3416-3429) ; Server/ts25zone/H01_MainApplication.h:79-80,
///     Server/ts25zone/S01_MainApplication.cpp:42-43 (guard-post slot-block sizing; <c>tGuardTribePostNum</c> =
///     <see cref="GuardPostsPerSegment" /> = 5) ; Server/ts25zone/S07_MyGame01.cpp:486-506 (corridor/hub zone
///     process start-up unconditionally forces its own owned checkpoint segment(s) open, independent of actual
///     guard-post liveness -- modeled below as "the very first <see cref="Simulate" /> call for this zone forces
///     its owned segments open instead of scanning", the same boot-pass idiom <see cref="TribeGuardSpawner" />
///     already uses for its own region pool; the deferred <c>return</c> on that first call means the forced-open
///     window genuinely survives until this same zone's NEXT tick, matching "until that same process's
///     tick-based re-derivation next runs").
/// </remarks>
public sealed class TribeGuardCorridorStateDerivationSystem(
    TribeGuardCorridorCatalog catalog,
    TribeGuardCorridorState state) : ISimulationSystem
{
    /// <summary><c>tGuardTribePostNum</c> -- fixed guard-post creature count defending one checkpoint.</summary>
    public const int GuardPostsPerSegment = 5;

    private readonly ConcurrentDictionary<short, CorridorZoneState> _stateByZone = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var owned = catalog.GetSegmentsOwnedByZone(zone.MapId);
        if (owned.Count == 0)
            return; // not one of the sixteen corridor zones or the hub -- unconditional no-op, every tick

        var zoneState = _stateByZone.GetOrAdd(zone.MapId, static _ => new CorridorZoneState());

        if (!zoneState.BootPassDone)
        {
            zoneState.BootPassDone = true;
            foreach (var (tribeId, segmentIndex) in owned)
                state.TrySetOpen(tribeId, segmentIndex, true);

            return; // startup override only this call -- live re-derivation begins on this zone's NEXT tick
        }

        foreach (var (tribeId, segmentIndex) in owned)
            EvaluateSegment(zone, tribeId, segmentIndex);
    }

    private void EvaluateSegment(Zone zone, byte tribeId, byte segmentIndex)
    {
        if (!catalog.TryGetGuardPostSlots(tribeId, segmentIndex, out var slots))
            return; // no configured guard-post slots yet for this segment -- documented catalog gap, safe no-op

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
        // else: already matches the just-computed result -- no-op, per the contract's own "no redundant
        // notification" rule.
    }
}
