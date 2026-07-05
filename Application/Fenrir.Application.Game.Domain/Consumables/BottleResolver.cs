using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Pure resolver for the aBottle/aBottleCount 10-slot array shared by CZ_USE_INVENTORY_ITEM_SEND's iSort==26
///     family (S04_MyWork03.cpp:2448, acquisition) and CZ_BOTTLE_STATE_SEND (S04_MyWork02.cpp:14763,
///     consumption). No I/O, no Zone dependency.
/// </summary>
public static class BottleResolver
{
    public enum AcquireOutcome
    {
        Rejected,
        Success
    }

    public enum DrinkOutcome
    {
        /// <summary>aBottle[index]==0 or aBottleCount[index]&lt;1 -- the legacy sends no reply at all here.</summary>
        Silent,

        /// <summary>tSort!=0, index out of [0,9], or a mount blocks drinking -- echoes a Result=1 failure.</summary>
        Rejected,

        Success
    }

    public const int SlotCount = 10;
    public const int RefillCount = 30;
    public const int DrunkDurationTicks = 120;

    /// <summary>
    ///     Mirrors the case-26 switch exactly: reuses (clears) an existing depleted slot for the same item id if
    ///     one exists (Quit()-worthy in the legacy if that slot still has stock), otherwise claims the first
    ///     empty slot found scanning from index 0.
    /// </summary>
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

    /// <summary>
    ///     Mount-blocks-drinking gate (!aAnimalNumber || aAnimalAbsorbState) always evaluates as "no mount" today
    ///     -- Fenrir has no Mount/AnimalAbsorb state on <c>PlayerRuntimeState</c> yet (Batch F), so this
    ///     degrades to always-unblocked, matching the observable behavior for every character until that state
    ///     exists.
    /// </summary>
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
