using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Runes;

public static class RuneSocketResolver
{
    public enum InsertOutcome
    {
        Rejected,
        Success
    }

    public enum RemoveOutcome
    {
        Rejected,
        InventoryFull,
        Success
    }

    public const int SlotCount = 4;
    public const int BaseItemId = 93514;

    public static InsertResult ResolveInsert(int runeIndex, int sourceItemId, ImmutableArray<int> runeSystem)
    {
        if (runeIndex is < 0 or >= SlotCount || sourceItemId is < BaseItemId or >= BaseItemId + SlotCount)
            return new InsertResult(InsertOutcome.Rejected);

        var naturalSlot = sourceItemId - BaseItemId;
        if (runeIndex != naturalSlot || runeSystem[runeIndex] != 0)
            return new InsertResult(InsertOutcome.Rejected);

        return new InsertResult(InsertOutcome.Success);
    }

    public static RemoveResult ResolveRemove(int runeIndex, ImmutableArray<int> runeSystem,
        bool hasFreeInventorySlot)
    {
        if (runeIndex is < 0 or >= SlotCount)
            return new RemoveResult(RemoveOutcome.Rejected);

        var occupant = runeSystem[runeIndex];
        if (occupant == 0 || occupant != BaseItemId + runeIndex)
            return new RemoveResult(RemoveOutcome.Rejected);

        return !hasFreeInventorySlot
            ? new RemoveResult(RemoveOutcome.InventoryFull)
            : new RemoveResult(RemoveOutcome.Success, occupant);
    }

    public readonly record struct InsertResult(InsertOutcome Outcome)
    {
        public bool Succeeded => Outcome == InsertOutcome.Success;
    }

    public readonly record struct RemoveResult(RemoveOutcome Outcome, int ItemId = 0)
    {
        public bool Succeeded => Outcome == RemoveOutcome.Success;
    }
}
