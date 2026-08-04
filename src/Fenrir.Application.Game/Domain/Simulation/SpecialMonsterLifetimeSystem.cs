using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class SpecialMonsterLifetimeSystem : IZoneClockSystem
{
    public void AdvanceClock(Zone zone)
    {
        zone.ExpireSpecialMonsters();
    }

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
    }
}
