using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class BuffExpirySystem : ISimulationSystem
{
    private const int ExpiredMarker = 2;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            if (state.IsMovingZone || state.OneSecondGateOpenCount <= 0)
                continue;

            TickPlayer(zone, state, state.OneSecondGateOpenCount);
        }
    }

    private static void TickPlayer(Zone zone, PlayerRuntimeState state, int periodsElapsed)
    {
        var buff = state.Buffs.Buff;
        var changedSlots = state.BuffChangeScratch;
        var anyChanged = false;

        for (var slot = 0; slot < BuffCatalog.SlotCount; slot++)
        {
            var valueIndex = slot * 2;
            var durationIndex = valueIndex + 1;
            var magnitude = buff[valueIndex];

            if (BuffCatalog.IsDurationCounted(slot) && magnitude >= 1 && buff[durationIndex] < 1)
            {
                ZeroSlot(buff, slot);
                MarkSlotExpired(changedSlots, slot, ref anyChanged);
                continue;
            }

            if (magnitude < 1)
                continue;

            var remainingDuration = buff[durationIndex] - periodsElapsed;
            if (remainingDuration >= 1)
            {
                buff[durationIndex] = remainingDuration;
                continue;
            }

            ZeroSlot(buff, slot);
            ClearExclusivityFlagOnExpiry(state, slot);
            MarkSlotExpired(changedSlots, slot, ref anyChanged);
        }

        if (anyChanged)
            zone.RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

    private static void ZeroSlot(int[] buff, int slot)
    {
        buff[slot * 2] = 0;
        buff[slot * 2 + 1] = 0;
    }

    private static void MarkSlotExpired(int[] changedSlots, int slot, ref bool anyChanged)
    {
        if (!anyChanged)
        {
            Array.Clear(changedSlots);
            anyChanged = true;
        }

        changedSlots[slot] = ExpiredMarker;
    }

    private static void ClearExclusivityFlagOnExpiry(PlayerRuntimeState state, int slot)
    {
        switch (slot)
        {
            case BuffCatalog.DarkAttack:
                state.DarkAttackKind = 0;
                break;
            case BuffCatalog.HitRatePotion:
                state.HitRateKind = 0;
                break;
            case BuffCatalog.DodgeRatePotion:
                state.DodgeRateKind = 0;
                break;
        }
    }
}
