using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public interface ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed);
}

public interface IZoneClockSystem : ISimulationSystem
{
    public void AdvanceClock(Zone zone);
}
