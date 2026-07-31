using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PlayTimeAccrualSystem : ISimulationSystem
{
    private const int PlayTime2StatSort = 16;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            state.PlayTimeAccrualTicks += legacyTicksElapsed;
            var minutesElapsed = state.PlayTimeAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
            if (minutesElapsed <= 0)
                continue;

            state.PlayTimeAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;
            state.PlayTime1 += minutesElapsed;
            state.PlayTime2 += minutesElapsed;
            state.PlayTime3 += minutesElapsed;
            state.PlayTimeEvent += minutesElapsed;

            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = PlayTime2StatSort, Value = state.PlayTime2, Value2 = 0 });
        }
    }
}
