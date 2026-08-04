using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class BuffExpirySystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (state.IsMovingZone || legacyTicksElapsed <= 0)
                continue;

            TickPlayer(zone, state, legacyTicksElapsed);
        }
    }

    private static void TickPlayer(Zone zone, PlayerRuntimeState state, int simulationTicks)
    {
        var changedSlots = state.BuffChangeScratch;
        if (!TimedBuffExpiry.Advance(state.Buffs.Buff, simulationTicks, changedSlots))
            return;

        state.RepairExpiredBuffRuntimeState(changedSlots);
        zone.RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }
}
