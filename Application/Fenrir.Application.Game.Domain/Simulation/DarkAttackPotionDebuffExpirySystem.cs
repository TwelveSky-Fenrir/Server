using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class DarkAttackPotionDebuffExpirySystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (state.IsMovingZone)
                continue;

            if (!state.IsUnderDarkAttackPotionDebuff)
                continue;

            state.DarkAttackDebuffAccumulatorTicks += legacyTicksElapsed;
            if (state.DarkAttackDebuffAccumulatorTicks < SimulationClock.DarkAttackPotionDebuffLegacyTicks)
                continue;

            zone.ClearDarkAttackPotionDebuff(state);
        }
    }
}
