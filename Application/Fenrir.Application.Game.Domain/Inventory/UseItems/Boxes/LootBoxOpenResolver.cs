using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

public static class LootBoxOpenResolver
{
    public enum Outcome
    {

                Success,

                InventoryFull,

                RewardNotFound
    }

        public static SingleOpenPlan OpenSingle(BoxRewardSpec spec, byte boxContainer, byte boxSlot, ItemStack boxStack,
        ImmutableDictionary<byte, ItemStack> page0, ImmutableDictionary<byte, ItemStack> page1,
        Func<int, byte?> resolveRewardSort, Random random, int today, Func<int>? rewardIdOverride = null,
        bool secondPageAccessible = true, Func<int, int>? resolveRewardSerial = null)
    {
        var rewardId = rewardIdOverride is not null ? rewardIdOverride() : spec.RollRewardId(random);

        if (resolveRewardSort(rewardId) is not { } rewardSort)
            return SingleOpenPlan.Failure(Outcome.RewardNotFound, rewardId);

        var quantity = BoxRewardPlacementResolver.ResolveQuantity(rewardSort, 1);

        var expireDate = 0;
        if (!quantity.IsStackable && spec.RentalDays > 0 && GameDate.TryAddDays(today, spec.RentalDays, out var e))
            expireDate = e;

        var serial = resolveRewardSerial?.Invoke(rewardId) ?? 0;
        var reward = new BoxRewardPlacementResolver.ResolvedReward(rewardId, quantity.Quantity,
            quantity.IsStackable, 0, 0, 0, 0, expireDate, serial);

        var placement = BoxRewardPlacementResolver.Resolve(reward, boxContainer, boxSlot, page0, page1,
            secondPageAccessible);
        if (!placement.Succeeded)
            return SingleOpenPlan.Failure(Outcome.InventoryFull, rewardId);

        var (newPage0, newPage1) = ApplySlot(page0, page1, placement.Container, placement.Slot,
            placement.NewStack);

        var boxRemaining = boxStack.Quantity - 1;
        ItemStack? boxAfter = boxRemaining > 0 ? boxStack with { Quantity = boxRemaining } : null;
        (newPage0, newPage1) = ApplySlot(newPage0, newPage1, boxContainer, boxSlot, boxAfter);

        return new SingleOpenPlan(Outcome.Success, rewardId, reward.Quantity, placement.Outcome,
            placement.Container, placement.Slot, placement.NewStack!.Value, boxRemaining, newPage0, newPage1);
    }

        public static BulkOpenPlan OpenBulk(BoxRewardSpec spec, byte boxContainer, byte boxSlot, ItemStack boxStack,
        ImmutableDictionary<byte, ItemStack> page0, ImmutableDictionary<byte, ItemStack> page1,
        Func<int, byte?> resolveRewardSort, Random random, int today, int requestedCount,
        Func<int>? rewardIdOverride = null, bool secondPageAccessible = true)
    {
        var maxByStock = Math.Min(boxStack.Quantity, BoxRewardPlacementResolver.MaxStackQuantity);
        if (maxByStock < 1)
            return BulkOpenPlan.Empty(page0, page1);

        var count = Math.Clamp(requestedCount, 1, maxByStock);

        var currentPage0 = page0;
        var currentPage1 = page1;
        var remainingBox = boxStack.Quantity;
        var opened = 0;
        var rewards = ImmutableArray.CreateBuilder<OpenedReward>();

        for (var i = 0; i < count && remainingBox > 0; i++)
        {
            var boxNow = boxStack with { Quantity = remainingBox };
            var plan = OpenSingle(spec, boxContainer, boxSlot, boxNow, currentPage0, currentPage1,
                resolveRewardSort, random, today, rewardIdOverride, secondPageAccessible);

            if (plan.Outcome != Outcome.Success)
                break;

            currentPage0 = plan.ProjectedPage0;
            currentPage1 = plan.ProjectedPage1;
            remainingBox = plan.BoxRemainingQuantity;
            opened++;
            rewards.Add(new OpenedReward(plan.RewardItemId, plan.RewardQuantity, plan.PlacementOutcome,
                plan.RewardContainer, plan.RewardSlot, plan.RewardStack));
        }

        return new BulkOpenPlan(opened, remainingBox, currentPage0, currentPage1, rewards.ToImmutable());
    }

    private static (ImmutableDictionary<byte, ItemStack> Page0, ImmutableDictionary<byte, ItemStack> Page1) ApplySlot(
        ImmutableDictionary<byte, ItemStack> page0, ImmutableDictionary<byte, ItemStack> page1, byte container,
        byte slot, ItemStack? value)
    {
        if (container == ContainerMatrix.InventoryPage0)
            return (Apply(page0, slot, value), page1);

        return (page0, Apply(page1, slot, value));
    }

    private static ImmutableDictionary<byte, ItemStack> Apply(ImmutableDictionary<byte, ItemStack> page, byte slot,
        ItemStack? value)
    {
        return value is { } stack ? page.SetItem(slot, stack) : page.Remove(slot);
    }

        public readonly record struct SingleOpenPlan(
        Outcome Outcome,
        int RewardItemId,
        int RewardQuantity,
        BoxRewardPlacementResolver.Outcome PlacementOutcome,
        byte RewardContainer,
        byte RewardSlot,
        ItemStack RewardStack,
        int BoxRemainingQuantity,
        ImmutableDictionary<byte, ItemStack> ProjectedPage0,
        ImmutableDictionary<byte, ItemStack> ProjectedPage1)
    {
        public bool Succeeded => Outcome == Outcome.Success;

        internal static SingleOpenPlan Failure(Outcome outcome, int rewardItemId)
        {
            return new SingleOpenPlan(outcome, rewardItemId, 0, BoxRewardPlacementResolver.Outcome.InventoryFull,
                0, 0, default, 0, ImmutableDictionary<byte, ItemStack>.Empty,
                ImmutableDictionary<byte, ItemStack>.Empty);
        }
    }

        public readonly record struct BulkOpenPlan(
        int OpenedCount,
        int BoxRemainingQuantity,
        ImmutableDictionary<byte, ItemStack> ProjectedPage0,
        ImmutableDictionary<byte, ItemStack> ProjectedPage1,
        ImmutableArray<OpenedReward> Rewards)
    {
        internal static BulkOpenPlan Empty(ImmutableDictionary<byte, ItemStack> page0,
            ImmutableDictionary<byte, ItemStack> page1)
        {
            return new BulkOpenPlan(0, 0, page0, page1, ImmutableArray<OpenedReward>.Empty);
        }
    }

        public readonly record struct OpenedReward(
        int RewardItemId,
        int RewardQuantity,
        BoxRewardPlacementResolver.Outcome PlacementOutcome,
        byte RewardContainer,
        byte RewardSlot,
        ItemStack RewardStack);
}
