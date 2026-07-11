using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Social.Trade;

public static class TradeCommitPlanner
{

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
