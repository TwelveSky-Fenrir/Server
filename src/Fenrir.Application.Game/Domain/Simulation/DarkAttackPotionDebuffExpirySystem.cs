using Fenrir.Application.Game.Domain.World;
namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class DarkAttackPotionDebuffExpirySystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var now = DateTime.UtcNow;

        foreach (var state in zone.Players)
        {
            if (state.IsMovingZone)
                continue;

            if (!state.IsUnderDarkAttackPotionDebuff)
                continue;

            if (now - state.DarkAttackDebuffActivatedAtUtc < SimulationClock.DarkAttackPotionDebuffDuration)
                continue;

            zone.ClearDarkAttackPotionDebuff(state);
        }
    }
}
