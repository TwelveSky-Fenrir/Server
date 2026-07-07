using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

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

    /// <summary>
    ///     Uses <paramref name="state" />'s own <see cref="PlayerRuntimeState.BuffChangeScratch" /> instead of
    ///     allocating a fresh <c>int[35]</c> per player per tick -- this is the whole-zone-population, every-
    ///     500ms-tick hot loop the buff-slot-write-and-notify-pattern behavior contract flags as the main cost
    ///     of the old per-call allocation. Preserves the original lazy-allocate-on-first-change posture: the
    ///     scratch buffer is only cleared (once) the first time a slot is actually found expired this call, and
    ///     no notification (nor the clear) happens at all for a player with nothing expiring this tick.
    /// </summary>
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
