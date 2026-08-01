using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class MountExpiryCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(state, legacyTicksElapsed);
    }

    private static void TickPlayer(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        state.MountExpiryCountdownAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = state.MountExpiryCountdownAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.MountExpiryCountdownAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        if (!MountExpiryPolicy.IsExpiredWhileMounted(state.AnimalIndex, state.AnimalTime))
            return;

        state.AnimalIndex = MountExpiryPolicy.Dismounted(state.AnimalIndex);
        state.AnimalNumber = 0;
        state.AnimalAbsorbState = 0;
        state.MountAutoDismountPending = true;
    }
}
