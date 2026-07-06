using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     The one shared "place a just-rolled loot-box reward into inventory" routine every box family in this
///     document funnels through, regardless of which weighted table produced the winning item id. No I/O, no
///     Zone dependency -- callers resolve the reward item id/stackability/rent-expiry first (catalog lookup),
///     then hand the already-resolved shape here.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:5271-5430 (<c>MyWork__SetBoxChangeInventoryItem99</c> --
///     reward lookup, stack-merge-vs-fresh-slot decision, box consumption) ; Server/ts25zone/S04_MyWork05.cpp:
///     5199-5269 (<c>BoxReward_IsStackable99</c>, <c>BoxReward_FindStackSlot99</c> -- stack-merge eligibility
///     and the box's-own-slot exclusion) ; Server/Header/Protocol/DEFINE.h:611 (999-per-stack ceiling).
/// </remarks>
public static class BoxRewardPlacementResolver
{
    public enum Outcome
    {
        /// <summary>Merged into an existing same-id stack elsewhere in inventory.</summary>
        Merged,

        /// <summary>Written fresh into a previously-empty slot.</summary>
        PlacedInEmptySlot,

        /// <summary>No existing mergeable stack and no empty slot anywhere -- box not consumed, no reward granted.</summary>
        InventoryFull
    }

    /// <summary><c>MAX_ITEM_DUPLICATION_NUM</c> (DEFINE.h:611).</summary>
    public const int MaxStackQuantity = 999;

    /// <summary>
    ///     Searches both inventory pages for a mergeable same-id stack first (excluding the box's own slot, per
    ///     <c>BoxReward_FindStackSlot99</c>); falling back to the first empty slot found, page0 before page1,
    ///     scanning slot 0 upward within each page.
    /// </summary>
    public static Result Resolve(ResolvedReward reward, byte boxContainer, byte boxSlot,
        ImmutableDictionary<byte, ItemStack> page0, ImmutableDictionary<byte, ItemStack> page1)
    {
        if (reward.IsStackable &&
            TryFindMergeSlot(reward, boxContainer, boxSlot, ContainerMatrix.InventoryPage0, page0, out var merge0))
            return merge0;

        if (reward.IsStackable &&
            TryFindMergeSlot(reward, boxContainer, boxSlot, ContainerMatrix.InventoryPage1, page1, out var merge1))
            return merge1;

        if (TryFindEmptySlot(ContainerMatrix.InventoryPage0, page0, out var empty0Slot))
            return new Result(Outcome.PlacedInEmptySlot, ContainerMatrix.InventoryPage0, empty0Slot,
                BuildStack(reward));

        if (TryFindEmptySlot(ContainerMatrix.InventoryPage1, page1, out var empty1Slot))
            return new Result(Outcome.PlacedInEmptySlot, ContainerMatrix.InventoryPage1, empty1Slot,
                BuildStack(reward));

        return new Result(Outcome.InventoryFull, 0, 0, null);
    }

    private static bool TryFindMergeSlot(ResolvedReward reward, byte boxContainer, byte boxSlot,
        byte searchContainer, ImmutableDictionary<byte, ItemStack> container, out Result result)
    {
        foreach (var (slot, stack) in container)
        {
            // BoxReward_FindStackSlot99: a stack sitting in the box's own slot is deliberately excluded, to
            // avoid merging into a slot about to be zeroed by the box's own removal.
            if (searchContainer == boxContainer && slot == boxSlot)
                continue;

            if (stack.ItemId != reward.ItemId)
                continue;

            var merged = stack.Quantity + reward.Quantity;
            if (merged > MaxStackQuantity)
                continue;

            result = new Result(Outcome.Merged, searchContainer, slot, stack with { Quantity = merged });
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryFindEmptySlot(byte container, ImmutableDictionary<byte, ItemStack> current,
        out byte emptySlot)
    {
        ContainerMatrix.TryGetMaxSlot(container, out var maxSlotInclusive);
        for (var slot = 0; slot <= maxSlotInclusive; slot++)
        {
            if (current.ContainsKey((byte)slot))
                continue;

            emptySlot = (byte)slot;
            return true;
        }

        emptySlot = 0;
        return false;
    }

    private static ItemStack BuildStack(ResolvedReward reward)
    {
        return new ItemStack(reward.ItemId, reward.Quantity, reward.Enchant, reward.Combine, reward.Refine,
            reward.Socket, 0, 0, 0, reward.ExpireDate, 0);
    }

    /// <summary>Already-resolved shape of the reward the caller rolled -- see this type's own remarks.</summary>
    public readonly record struct ResolvedReward(
        int ItemId,
        int Quantity,
        bool IsStackable,
        byte Enchant,
        byte Combine,
        byte Refine,
        byte Socket,
        int ExpireDate);

    /// <summary>NewStack is null only for <see cref="Outcome.InventoryFull" />.</summary>
    public readonly record struct Result(Outcome Outcome, byte Container, byte Slot, ItemStack? NewStack)
    {
        public bool Succeeded => Outcome is Outcome.Merged or Outcome.PlacedInEmptySlot;
    }
}
