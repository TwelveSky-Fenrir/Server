using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeCommitPlanner
{
    public enum CommitRejection
    {
        None,

        InventoryOverflow,

        StaleReservation,

        UnsupportedOrigin
    }

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
            {
                if (!TryConsumeReservation(ref page0, slot.Slot, slot.Stack))
                    return Reject(currentPage0, currentPage1, CommitRejection.StaleReservation);
            }
            else if (slot.Container == ContainerMatrix.InventoryPage1)
            {
                if (!TryConsumeReservation(ref page1, slot.Slot, slot.Stack))
                    return Reject(currentPage0, currentPage1, CommitRejection.StaleReservation);
            }
            else
            {
                return Reject(currentPage0, currentPage1, CommitRejection.UnsupportedOrigin);
            }
        }

        foreach (var received in receivedSlots)
        {
            if (received is not { } slot)
                continue;

            if (TryFindFreeSlot(page0, out var freeSlot))
                page0 = page0.SetItem(freeSlot, slot.Stack);
            else if (TryFindFreeSlot(page1, out freeSlot))
                page1 = page1.SetItem(freeSlot, slot.Stack);
            else
                return Reject(currentPage0, currentPage1, CommitRejection.InventoryOverflow);
        }

        return new Plan(page0, page1, CommitRejection.None);
    }

    private static bool TryConsumeReservation(ref ImmutableDictionary<byte, ItemStack> page, byte slot,
        ItemStack reserved)
    {
        if (reserved.Quantity <= 0 || !page.TryGetValue(slot, out var live))
            return false;

        if (live.ItemId != reserved.ItemId || live.Quantity < reserved.Quantity)
            return false;

        var remaining = live.Quantity - reserved.Quantity;
        page = remaining > 0 ? page.SetItem(slot, live with { Quantity = remaining }) : page.Remove(slot);
        return true;
    }

    private static Plan Reject(ImmutableDictionary<byte, ItemStack> page0,
        ImmutableDictionary<byte, ItemStack> page1, CommitRejection rejection)
    {
        return new Plan(page0, page1, rejection);
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
        CommitRejection Rejection)
    {
        public bool Overflowed => Rejection != CommitRejection.None;
    }
}
