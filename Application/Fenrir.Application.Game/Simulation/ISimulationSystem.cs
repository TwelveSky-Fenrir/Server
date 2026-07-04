using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     One autonomous slice of per-zone simulation (monster IA, buff countdown, meditation regen, spawn
///     scheduling — the Phase C systems, per ServerDocs/30_Fenrir_ServerLogic/05 §0's deterministic legacy
///     order sessions → avatars → monstres → items → événements → spawns). Each <see cref="Zone" /> owns an
///     ORDERED list of systems and runs them in sequence inside its own tick — never from any other thread —
///     so a system may freely mutate zone state through the reference it is handed (single-writer invariant
///     preserved by construction).
/// </summary>
public interface ISimulationSystem
{
    /// <summary>
    ///     Advances this system by <paramref name="legacyTicksElapsed" /> legacy ticks of 500 ms each
    ///     (<see cref="SimulationClock" /> — NOT network frames). Called from the zone tick only when at least one
    ///     whole legacy tick is due, i.e. roughly twice per second; a burst greater than 1 means the host
    ///     stalled and the system must catch up (decrement multi-tick timers by the full amount, not by 1).
    /// </summary>
    public void Simulate(Zone zone, int legacyTicksElapsed);
}
