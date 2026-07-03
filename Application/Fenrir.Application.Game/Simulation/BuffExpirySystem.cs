using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Buff/debuff countdown (report 05 §7 point 4, verified <c>AVATAR_OBJECT::Update</c>): every occupied
///     BUFF_INFO slot's duration (<c>gBuff[slot][1]</c>, counted in legacy ticks) is decremented once per
///     legacy tick; a slot reaching zero is cleared (value AND duration both zeroed, matching "slot vidé").
///     Any player with at least one slot expiring this frame gets <see cref="PlayerRuntimeState.Stats" />
///     recomputed once and a single <c>ZC_AVATAR_EFFECT_VALUE_INFO</c> broadcast covering every slot that
///     changed -- see <c>Zone.RecomputeStatsAndBroadcastBuffs</c>'s own remarks for why the recompute is a
///     Fenrir-specific necessity (an event-driven stats CACHE) rather than a legacy transcription.
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
                continue; // slot not occupied.

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
