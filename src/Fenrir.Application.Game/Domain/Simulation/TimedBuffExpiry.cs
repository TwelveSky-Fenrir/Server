using Fenrir.Application.Game.Domain.Buffs;

namespace Fenrir.Application.Game.Domain.Simulation;

public static class TimedBuffExpiry
{
    public static bool Advance(Span<int> buff, int simulationTicks, Span<int> changedSlots)
    {
        if (simulationTicks <= 0)
            return false;

        var anyExpired = false;

        for (var slot = 0; slot < BuffCatalog.SlotCount; slot++)
        {
            if (!BuffCatalog.IsDurationCounted(slot))
                continue;

            var valueIndex = slot * 2;
            var durationIndex = valueIndex + 1;
            if (buff[valueIndex] < 1)
                continue;

            var remainingDuration = buff[durationIndex] - simulationTicks;
            if (remainingDuration >= 1)
            {
                buff[durationIndex] = remainingDuration;
                continue;
            }

            buff[valueIndex] = 0;
            buff[durationIndex] = 0;

            if (!anyExpired)
            {
                changedSlots.Clear();
                anyExpired = true;
            }

            changedSlots[slot] = BuffCatalog.RemovedStateMarker;
        }

        return anyExpired;
    }
}
