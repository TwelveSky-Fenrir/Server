using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class AvatarOneSecondGateSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            state.OneSecondGateOpenCount = 0;

        for (var tick = 0; tick < legacyTicksElapsed; tick++)
        {
            var rawTick = zone.AdvanceRawLogicTick();

            foreach (var state in zone.Players)
            {
                if (rawTick - state.LastOneSecondGateTick != SimulationClock.OneSecondGateLegacyTicks)
                    continue;

                state.LastOneSecondGateTick = rawTick;
                state.OneSecondGateOpenCount++;
            }
        }
    }
}
