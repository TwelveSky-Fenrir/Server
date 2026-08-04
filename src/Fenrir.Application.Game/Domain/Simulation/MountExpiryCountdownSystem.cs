using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class MountExpiryCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(zone, state, legacyTicksElapsed);
    }

    private static void TickPlayer(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed)
    {
        state.MountActivityDecayAccrualTicks += legacyTicksElapsed;
        var activityPulses = state.MountActivityDecayAccrualTicks / SimulationClock.MountActivityDecayLegacyTicks;
        if (activityPulses > 0)
        {
            state.MountActivityDecayAccrualTicks -= activityPulses * SimulationClock.MountActivityDecayLegacyTicks;
            zone.AdvanceMountActivity(state, activityPulses);
        }

        state.MountExpiryCountdownAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = state.MountExpiryCountdownAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.MountExpiryCountdownAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        zone.AdvanceMountExpiry(state, minutesElapsed);
    }
}
