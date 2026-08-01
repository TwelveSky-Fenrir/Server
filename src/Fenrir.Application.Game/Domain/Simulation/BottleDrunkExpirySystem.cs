using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class BottleDrunkExpirySystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(zone, state, legacyTicksElapsed);
    }

    private static void TickPlayer(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed)
    {
        if (state.DrunkBottleTicksRemaining <= 0)
            return;

        var remaining = state.DrunkBottleTicksRemaining - legacyTicksElapsed;
        if (remaining > 0)
        {
            state.DrunkBottleTicksRemaining = remaining;
            return;
        }

        zone.ExpireDrunkBottleEffect(state);
    }
}
