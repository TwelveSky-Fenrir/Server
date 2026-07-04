using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Buff/debuff countdown (AVATAR_OBJECT::Update): each occupied BUFF_INFO slot's duration is decremented
///     once per legacy tick; a slot reaching zero is cleared. Stats are recomputed once per player per frame,
///     covering every slot that expired that frame.
/// </summary>
public sealed class BuffExpirySystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(zone, state, legacyTicksElapsed);
    }

    private static void TickPlayer(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed)
    {
        int[]? changedSlots = null;

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
            (changedSlots ??= new int[35])[slot] = 1;
        }

        if (changedSlots is not null)
            zone.RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }
}
