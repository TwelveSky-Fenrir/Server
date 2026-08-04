using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class StunCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (!state.IsStunned || legacyTicksElapsed <= 0)
                continue;

            state.StunDurationTicks -= legacyTicksElapsed;

            if (state.StunDurationTicks <= 0)
            {
                zone.ClearStun(state);
                continue;
            }

            if (state.OneSecondGateOpenCount > 0)
                zone.BroadcastStunActionState(state, state.StunDurationTicks);
        }
    }
}
