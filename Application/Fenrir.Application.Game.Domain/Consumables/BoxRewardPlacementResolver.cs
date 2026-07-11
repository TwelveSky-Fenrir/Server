using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class BoxRewardPlacementResolver
{
    public enum Outcome
    {

                Merged,

                PlacedInEmptySlot,

                InventoryFull
    }

        public const int MaxStackQuantity = 999;

        public const byte PetSort = 22;

        public const int MaxPetActivity = 100;

        public static QuantityResult ResolveQuantity(byte rewardSort, int rolledQuantity)
    {
        if (ContainerMatrix.IsStackableSort(rewardSort))
            return new QuantityResult(Math.Clamp(rolledQuantity, 1, MaxStackQuantity), true);

        if (rewardSort == PetSort)
            return new QuantityResult(Math.Clamp(rolledQuantity, 0, MaxPetActivity), false);

        return new QuantityResult(0, false);
    }

        public static Result Resolve(ResolvedReward reward, byte boxContainer, byte boxSlot,
        ImmutableDictionary<byte, ItemStack> page0, ImmutableDictionary<byte, ItemStack> page1,
        bool secondPageAccessible = true)
    {
        if (reward.IsStackable &&
            TryFindMergeSlot(reward, boxContainer, boxSlot, ContainerMatrix.InventoryPage0, page0, out var merge0))
            return merge0;

        if (secondPageAccessible && reward.IsStackable &&
            TryFindMergeSlot(reward, boxContainer, boxSlot, ContainerMatrix.InventoryPage1, page1, out var merge1))
            return merge1;

        if (TryFindEmptySlot(ContainerMatrix.InventoryPage0, page0, out var empty0Slot))
            return new Result(Outcome.PlacedInEmptySlot, ContainerMatrix.InventoryPage0, empty0Slot,
                BuildStack(reward));

        if (secondPageAccessible && TryFindEmptySlot(ContainerMatrix.InventoryPage1, page1, out var empty1Slot))
            return new Result(Outcome.PlacedInEmptySlot, ContainerMatrix.InventoryPage1, empty1Slot,
                BuildStack(reward));

        return new Result(Outcome.InventoryFull, 0, 0, null);
    }

    private static bool TryFindMergeSlot(ResolvedReward reward, byte boxContainer, byte boxSlot,
        byte searchContainer, ImmutableDictionary<byte, ItemStack> container, out Result result)
    {
        foreach (var (slot, stack) in container)
        {
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
        var serial = reward.IsStackable ? 0 : reward.Serial;
        return new ItemStack(reward.ItemId, reward.Quantity, reward.Enchant, reward.Combine, reward.Refine,
            reward.Socket, 0, 0, 0, reward.ExpireDate, serial);
    }

        public readonly record struct QuantityResult(int Quantity, bool IsStackable);

        public readonly record struct ResolvedReward(
        int ItemId,
        int Quantity,
        bool IsStackable,
        byte Enchant,
        byte Combine,
        byte Refine,
        byte Socket,
        int ExpireDate,
        int Serial = 0);

        public readonly record struct Result(Outcome Outcome, byte Container, byte Slot, ItemStack? NewStack)
    {
        public bool Succeeded => Outcome is Outcome.Merged or Outcome.PlacedInEmptySlot;
    }
}
