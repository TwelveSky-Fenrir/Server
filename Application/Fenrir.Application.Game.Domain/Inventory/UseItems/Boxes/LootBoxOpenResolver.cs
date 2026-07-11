using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

/// <summary>
///     The pure, I/O-free core of a loot-box open (workstream C10): roll a reward from the box's
///     <see cref="BoxRewardSpec" />, clamp its quantity by sort, place it (or report inventory-full), and
///     project the resulting inventory pages -- with the box's own single-unit consumption folded into the same
///     projection so a reward is never granted without exactly one box being consumed, and never the reverse.
///     No repository, no <c>Zone</c>, no logging: <c>LootBoxUseItemHandler</c> owns the durable write, the
///     mirror, and the audit around this decision, exactly the split every other C10/C9 resolver keeps.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:5150-5431 (<c>SetItemQuantity</c> +
///     <c>
///         MyWork__SetBoxChange
///         InventoryItem99
///     </c>
///     : quantity clamp, stackable strip, merge-vs-empty-slot placement, inventory-full
///     without consuming, one-box consume) ; Server/ts25zone/S04_MyWork03.cpp:1783-1854 (<c>BulkOpenBoxNoStellar</c>:
///     count clamp, one-at-a-time loop, no-progress early stop).
///     <para>
///         Known deliberate divergence (fail-safe, flagged): the legacy's "write the reward into the box's own
///         slot when the box stack is down to its last unit" fallback is NOT modeled -- if the ONLY free space
///         is the box's own last-unit slot, this reports inventory-full (box kept) instead of reclaiming that
///         slot. <see cref="BoxRewardPlacementResolver" /> excludes the box's own occupied slot from both the
///         merge scan and the empty-slot scan; reproducing the reclaim would be a behavioral change to that
///         already-tested primitive, so it is left as a narrow, safe over-rejection.
///     </para>
/// </remarks>
public static class LootBoxOpenResolver
{
    public enum Outcome
    {
        /// <summary>Reward rolled, placed, and exactly one box consumed -- see the projected pages.</summary>
        Success,

        /// <summary>No mergeable stack and no empty slot: box NOT consumed, nothing granted (result 3).</summary>
        InventoryFull,

        /// <summary>The rolled reward id is not a known item: box NOT consumed, nothing granted (result 1).</summary>
        RewardNotFound
    }

    /// <summary>
    ///     Opens exactly one box. <paramref name="resolveRewardSort" /> returns the reward item's world.Items
    ///     Sort, or null if the id is unknown to the item master. <paramref name="today" /> is the compact
    ///     (YYYYMMDD) date a rental reward's expiry is projected from (via <see cref="GameDate.TryAddDays" />).
    ///     <paramref name="secondPageAccessible" /> is C1-vault-expiry-enforcement's placement-gate input --
    ///     see <see cref="BoxRewardPlacementResolver.Resolve" />'s own remarks; defaults to
    ///     <see langword="true" /> so every pre-existing caller/test keeps searching both pages unconditionally.
    /// </summary>
    public static SingleOpenPlan OpenSingle(BoxRewardSpec spec, byte boxContainer, byte boxSlot, ItemStack boxStack,
        ImmutableDictionary<byte, ItemStack> page0, ImmutableDictionary<byte, ItemStack> page1,
        Func<int, byte?> resolveRewardSort, Random random, int today, Func<int>? rewardIdOverride = null,
        bool secondPageAccessible = true)
    {
        // rewardIdOverride lets a caller substitute a pity-gated roll (e.g. CloakBoxRewardTable.Roll) for the
        // box's own pure spec.RollRewardId -- the spec itself is a process-wide singleton that cannot hold any
        // per-avatar pity state. Optional/defaulted so every non-pity box's existing call sites are unaffected.
        var rewardId = rewardIdOverride is not null ? rewardIdOverride() : spec.RollRewardId(random);

        if (resolveRewardSort(rewardId) is not { } rewardSort)
            return SingleOpenPlan.Failure(Outcome.RewardNotFound, rewardId);

        var quantity = BoxRewardPlacementResolver.ResolveQuantity(rewardSort, 1);

        // Stackable rewards are stripped of value/serial/socket/expiry; a rental applies only to a non-stackable
        // reward's expiry date.
        var expireDate = 0;
        if (!quantity.IsStackable && spec.RentalDays > 0 && GameDate.TryAddDays(today, spec.RentalDays, out var e))
            expireDate = e;

        var reward = new BoxRewardPlacementResolver.ResolvedReward(rewardId, quantity.Quantity,
            quantity.IsStackable, 0, 0, 0, 0, expireDate);

        var placement = BoxRewardPlacementResolver.Resolve(reward, boxContainer, boxSlot, page0, page1,
            secondPageAccessible);
        if (!placement.Succeeded)
            return SingleOpenPlan.Failure(Outcome.InventoryFull, rewardId);

        // Reward placement never targets the box's own slot (excluded from both merge and empty-slot scans), so
        // applying the placement and the box's single-unit consumption to the same page pair never collides.
        var (newPage0, newPage1) = ApplySlot(page0, page1, placement.Container, placement.Slot,
            placement.NewStack);

        var boxRemaining = boxStack.Quantity - 1;
        ItemStack? boxAfter = boxRemaining > 0 ? boxStack with { Quantity = boxRemaining } : null;
        (newPage0, newPage1) = ApplySlot(newPage0, newPage1, boxContainer, boxSlot, boxAfter);

        return new SingleOpenPlan(Outcome.Success, rewardId, reward.Quantity, placement.Outcome,
            placement.Container, placement.Slot, placement.NewStack!.Value, boxRemaining, newPage0, newPage1);
    }

    /// <summary>
    ///     Shift+right-click bulk open (<c>BulkOpenBoxNoStellar</c>): clamps the requested count to
    ///     1..min(box stack, <see cref="BoxRewardPlacementResolver.MaxStackQuantity" />), then opens one box at a
    ///     time, threading each open's projected pages into the next, stopping early the moment an open does not
    ///     succeed (inventory full or any other non-success -- the no-progress guard that prevents an infinite
    ///     loop) or the box stack is depleted. <paramref name="secondPageAccessible" /> is threaded unchanged
    ///     into every <see cref="OpenSingle" /> call -- see that method's own remarks.
    /// </summary>
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

            // No-progress stop: a full inventory or any other non-success halts the whole bulk request, leaving
            // the boxes opened so far applied and the rest untouched.
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

    /// <summary>One box's open outcome and, on success, everything the handler needs to persist/mirror/audit it.</summary>
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

    /// <summary>Aggregate result of a bulk open: how many boxes actually opened and the final projected pages.</summary>
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

    /// <summary>One reward granted within a bulk open, for the per-open audit trail.</summary>
    public readonly record struct OpenedReward(
        int RewardItemId,
        int RewardQuantity,
        BoxRewardPlacementResolver.Outcome PlacementOutcome,
        byte RewardContainer,
        byte RewardSlot,
        ItemStack RewardStack);
}
