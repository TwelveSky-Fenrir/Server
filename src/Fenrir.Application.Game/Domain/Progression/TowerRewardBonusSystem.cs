using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Progression;

public sealed class TowerRewardBonusSystem(TowerWarState towerWar) : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        _ = zone;
        _ = legacyTicksElapsed;
        towerWar.RecomputeTribeBonuses();
    }
}
