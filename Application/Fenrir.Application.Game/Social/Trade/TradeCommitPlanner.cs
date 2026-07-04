using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Social.Trade;

/// <summary>
///     Pure logic that projects a completed TradeSession onto the final InventoryPage0/Page1 contents: this
///     side's offered slots are removed, the other side's offered items fill the first free slots (page 0
///     then page 1).
/// </summary>
public static class TradeCommitPlanner
{
    /// <summary>Plan.Overflowed means the receiving side had no free slot for one or more incoming items -- the caller must abort the whole trade, never drop an item silently.</summary>
    public static Plan BuildFinalContainers(
        ImmutableDictionary<byte, ItemStack> currentPage0,
        ImmutableDictionary<byte, ItemStack> currentPage1,
        IReadOnlyList<(byte Container, byte Slot, ItemStack Stack)?> ownOfferedSlots,
        IReadOnlyList<(byte Container, byte Slot, ItemStack Stack)?> receivedSlots)
    {
        var page0 = currentPage0;
        var page1 = currentPage1;

        foreach (var offered in ownOfferedSlots)
        {
            if (offered is not { } slot)
                continue;

            if (slot.Container == ContainerMatrix.InventoryPage0)
                page0 = page0.Remove(slot.Slot);
            else if (slot.Container == ContainerMatrix.InventoryPage1)
                page1 = page1.Remove(slot.Slot);
        }

        var overflowed = false;
        foreach (var received in receivedSlots)
        {
            if (received is not { } slot)
                continue;

            if (TryFindFreeSlot(page0, out var freeSlot))
                page0 = page0.SetItem(freeSlot, slot.Stack);
            else if (TryFindFreeSlot(page1, out freeSlot))
                page1 = page1.SetItem(freeSlot, slot.Stack);
            else
                overflowed = true;
        }

        return new Plan(page0, page1, overflowed);
    }

    private static bool TryFindFreeSlot(ImmutableDictionary<byte, ItemStack> container, out byte freeSlot)
    {
        for (var i = 0; i <= 63; i++)
        {
            if (container.ContainsKey((byte)i))
                continue;

            freeSlot = (byte)i;
            return true;
        }

        freeSlot = 0;
        return false;
    }

    public readonly record struct Plan(
        ImmutableDictionary<byte, ItemStack> Page0,
        ImmutableDictionary<byte, ItemStack> Page1,
        bool Overflowed);
}
