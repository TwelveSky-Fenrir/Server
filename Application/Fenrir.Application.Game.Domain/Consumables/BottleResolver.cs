using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class BottleResolver
{
    public enum AcquireOutcome
    {
        Rejected,
        Success
    }

    public enum DrinkOutcome
    {
        Silent,

        Rejected,

        Success
    }

    public const int SlotCount = 10;
    public const int RefillCount = 30;
    public const int DrunkDurationTicks = 120;

    public static AcquireResult ResolveAcquire(ImmutableArray<(int ItemId, int Count)> slots, int itemId)
    {
        var existingIndex = -1;
        for (var i = 0; i < slots.Length; i++)
            if (slots[i].ItemId == itemId)
            {
                existingIndex = i;
                break;
            }

        if (existingIndex != -1 && slots[existingIndex].Count > 0)
            return new AcquireResult(AcquireOutcome.Rejected, 0, 0);

        var emptyIndex = -1;
        for (var i = 0; i < slots.Length; i++)
        {
            var candidateItemId = i == existingIndex ? 0 : slots[i].ItemId;
            if (candidateItemId != 0) continue;
            emptyIndex = i;
            break;
        }

        return emptyIndex == -1
            ? new AcquireResult(AcquireOutcome.Rejected, 0, 0)
            : new AcquireResult(AcquireOutcome.Success, emptyIndex, RefillCount);
    }

    public static DrinkResult ResolveDrink(ImmutableArray<(int ItemId, int Count)> slots, int sort, int index)
    {
        if (sort != 0 || index < 0 || index >= slots.Length)
            return new DrinkResult(DrinkOutcome.Rejected, 0);

        if (slots[index].ItemId == 0 || slots[index].Count < 1)
            return new DrinkResult(DrinkOutcome.Silent, 0);

        return new DrinkResult(DrinkOutcome.Success, slots[index].Count - 1);
    }

    public readonly record struct AcquireResult(AcquireOutcome Outcome, int SlotIndex, int RefilledCount);

    public readonly record struct DrinkResult(DrinkOutcome Outcome, int NewCount);
}
