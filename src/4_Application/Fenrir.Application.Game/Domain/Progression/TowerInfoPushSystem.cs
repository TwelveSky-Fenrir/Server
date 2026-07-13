using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Progression;

public sealed class TowerInfoPushSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        zone.TickTowerInfoPush(legacyTicksElapsed);
    }
}
