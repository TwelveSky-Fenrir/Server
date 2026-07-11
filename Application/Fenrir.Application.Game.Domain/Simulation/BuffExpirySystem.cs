using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class BuffExpirySystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(zone, state, legacyTicksElapsed);
    }

        private static void TickPlayer(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed)
    {
        var changedSlots = state.BuffChangeScratch;
        var anyChanged = false;

        for (var slot = 0; slot < 35; slot++)
        {
            var durationIndex = slot * 2 + 1;
            var remaining = state.Buffs.Buff[durationIndex];
            if (remaining <= 0)
                continue;

            remaining -= legacyTicksElapsed;
            if (remaining > 0)
            {
                state.Buffs.Buff[durationIndex] = remaining;
                continue;
            }

            state.Buffs.Buff[slot * 2] = 0;
            state.Buffs.Buff[durationIndex] = 0;

            if (!anyChanged)
            {
                Array.Clear(changedSlots);
                anyChanged = true;
            }

            changedSlots[slot] = 1;
        }

        if (anyChanged)
            zone.RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }
}
