using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Social.Trade;

/// <summary>
///     Pure logic (no I/O, no <c>PlayerRuntimeState</c>/<c>Zone</c> dependency, same posture as
///     <c>ContainerMatrix</c>) that projects a completed <see cref="TradeSession" /> onto the FINAL
///     InventoryPage0/InventoryPage1 contents each side needs after the exchange: this side's own
///     offered slots are removed (they are being given away), the OTHER side's offered items are added
///     into this side's first available free slots (across page 0 then page 1). Independently testable
///     without any wire/DB dependency -- see <see cref="TradeRegistry" />'s own remarks on why the offer
///     slots are currently always empty in production (the tSort 218-222 wiring gap).
/// </summary>
public static class TradeCommitPlanner
{
    public readonly record struct Plan(
        ImmutableDictionary<byte, ItemStack> Page0,
        ImmutableDictionary<byte, ItemStack> Page1,
        bool Overflowed);

    /// <summary>
    ///     <paramref name="overflowed" /> (via the returned <see cref="Plan.Overflowed" />) signals the
    ///     receiving side had no free slot left for one or more incoming items -- the caller must treat
    ///     this as a commit-time failure (abort the WHOLE trade, per D7 "no partial commit"): silently
    ///     dropping an item would be a value loss, and partially applying the trade would violate
    ///     atomicity just as much as a mid-write SQL fault would.
    /// </summary>
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
}
