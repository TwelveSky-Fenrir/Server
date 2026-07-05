using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Runes;

/// <summary>
///     Pure resolver for CZ_RUNE_SYSTEM_SEND (op157, S04_MyWork03.cpp:8162). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Legacy quirk, reproduced not fixed (D8): on insert (sort 0), the stored rune "item id"
///     (<c>wAvatar.aRuneSystem[tRuneIndex] = r-&gt;tItemIndex</c>) comes from the CLIENT-supplied
///     <c>ItemIndex</c> wire field, not from the actual inventory slot's item id that was just validated --
///     <see cref="ResolveInsert" /> mirrors this by taking the client value as an opaque pass-through, only
///     using the inventory-derived id for validation. There is no default case in the legacy's
///     <c>switch(tSort)</c>: any sort other than 0/1 is silently ignored (no reply, no disconnect), unlike most
///     other opcodes in this codebase.
/// </remarks>
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

    /// <summary>
    ///     <paramref name="sourceItemId" /> is the item actually sitting in the source inventory slot (used only
    ///     for validation); the value ultimately written to the rune slot is the caller's own client-supplied
    ///     item id (see remarks).
    /// </summary>
    public static InsertResult ResolveInsert(int runeIndex, int sourceItemId, ImmutableArray<int> runeSystem)
    {
        if (runeIndex is < 0 or >= SlotCount || sourceItemId is < BaseItemId or >= BaseItemId + SlotCount ||
            runeSystem[runeIndex] != 0)
            return new InsertResult(InsertOutcome.Rejected);

        var naturalSlot = sourceItemId - BaseItemId;
        return runeSystem[naturalSlot] != 0
            ? new InsertResult(InsertOutcome.Rejected)
            : new InsertResult(InsertOutcome.Success);
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
