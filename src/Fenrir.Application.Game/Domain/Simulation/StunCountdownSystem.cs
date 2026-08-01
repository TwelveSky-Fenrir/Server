using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class StunCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (!state.IsStunned || state.OneSecondGateOpenCount <= 0)
                continue;

            state.StunDurationSeconds -= state.OneSecondGateOpenCount;

            if (state.StunDurationSeconds <= 0)
            {
                zone.ClearStun(state);
                continue;
            }

            state.CanUseConsumables = false;
        }
    }
}
