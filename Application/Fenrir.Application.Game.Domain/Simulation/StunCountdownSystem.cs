using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class StunCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (!state.IsStunned)
                continue;

            state.StunCountdownAccumulatorTicks += legacyTicksElapsed;
            var secondsElapsed = state.StunCountdownAccumulatorTicks / SimulationClock.StunCountdownLegacyTicks;
            if (secondsElapsed <= 0)
                continue;

            state.StunCountdownAccumulatorTicks -= secondsElapsed * SimulationClock.StunCountdownLegacyTicks;
            state.StunDurationSeconds -= secondsElapsed;

            if (state.StunDurationSeconds <= 0)
                zone.ClearStun(state);
        }
    }
}
