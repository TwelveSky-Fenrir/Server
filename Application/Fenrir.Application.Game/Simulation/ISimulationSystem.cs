using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     One autonomous slice of per-zone simulation (monster IA, buff countdown, meditation regen, spawn
///     scheduling). Each Zone owns an ordered list of systems and runs them in sequence inside its own tick,
///     never from any other thread.
/// </summary>
public interface ISimulationSystem
{
    /// <summary>
    ///     Advances by <paramref name="legacyTicksElapsed" /> legacy ticks of 500 ms each. A burst greater than
    ///     1 means the host stalled and the system must catch up (decrement multi-tick timers by the full
    ///     amount, not by 1).
    /// </summary>
    public void Simulate(Zone zone, int legacyTicksElapsed);
}
