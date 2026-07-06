using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Consumables;

public class BoxRewardPlacementResolverTests
{
    private static readonly ItemStack Filler = new(999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);

    private static BoxRewardPlacementResolver.ResolvedReward Reward(int itemId, int quantity, bool stackable)
    {
        return new BoxRewardPlacementResolver.ResolvedReward(itemId, quantity, stackable, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void Resolve_ExistingStackElsewhere_Merges()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(0, new ItemStack(200, 5, 0, 0, 0, 0, 0, 0, 0, 0, 1)) // box's own slot
            .SetItem(3, new ItemStack(500, 10, 0, 0, 0, 0, 0, 0, 0, 0, 2)); // existing reward stack

        var result = BoxRewardPlacementResolver.Resolve(Reward(500, 4, true), 0, 0,
            page0, ImmutableDictionary<byte, ItemStack>.Empty);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.Merged, result.Outcome);
        Assert.Equal(0, result.Container);
        Assert.Equal(3, result.Slot);
        Assert.Equal(14, result.NewStack!.Value.Quantity);
    }

    [Fact]
    public void Resolve_MatchingStackSitsInTheBoxsOwnSlot_IsExcludedFromMerge_FallsToEmptySlot()
    {
        // The box itself happens to share an id with the reward -- must not merge into the slot the box's own
        // removal is about to zero out.
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(0, new ItemStack(500, 5, 0, 0, 0, 0, 0, 0, 0, 0, 1));

        var result = BoxRewardPlacementResolver.Resolve(Reward(500, 1, true), 0, 0,
            page0, ImmutableDictionary<byte, ItemStack>.Empty);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, result.Outcome);
        Assert.Equal(0, result.Container);
        Assert.Equal(1, result.Slot);
    }

    [Fact]
    public void Resolve_MergeWouldExceedStackCeiling_SkipsToEmptySlotInstead()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(0, Filler) // box's own slot
            .SetItem(1, new ItemStack(500, 998, 0, 0, 0, 0, 0, 0, 0, 0, 2));

        var result = BoxRewardPlacementResolver.Resolve(Reward(500, 5, true), 0, 0,
            page0, ImmutableDictionary<byte, ItemStack>.Empty);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, result.Outcome);
        Assert.Equal(2, result.Slot);
    }

    [Fact]
    public void Resolve_NoExistingStack_PlacesInFirstEmptySlot()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Filler);

        var result = BoxRewardPlacementResolver.Resolve(Reward(700, 1, true), 0, 0,
            page0, ImmutableDictionary<byte, ItemStack>.Empty);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, result.Outcome);
        Assert.Equal(0, result.Container);
        Assert.Equal(1, result.Slot);
        Assert.Equal(700, result.NewStack!.Value.ItemId);
    }

    [Fact]
    public void Resolve_Page0Full_FallsThroughToPage1()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        ContainerMatrix.TryGetMaxSlot(ContainerMatrix.InventoryPage0, out var maxSlot);
        for (var slot = 0; slot <= maxSlot; slot++)
            builder[(byte)slot] = Filler;
        var fullPage0 = builder.ToImmutable();

        var result = BoxRewardPlacementResolver.Resolve(Reward(700, 1, false), 0, 0,
            fullPage0, ImmutableDictionary<byte, ItemStack>.Empty);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, result.Outcome);
        Assert.Equal(ContainerMatrix.InventoryPage1, result.Container);
        Assert.Equal(0, result.Slot);
    }

    [Fact]
    public void Resolve_BothPagesFull_ReportsInventoryFull_AndGrantsNothing()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        ContainerMatrix.TryGetMaxSlot(ContainerMatrix.InventoryPage0, out var maxSlot);
        for (var slot = 0; slot <= maxSlot; slot++)
            builder[(byte)slot] = Filler;
        var full = builder.ToImmutable();

        var result = BoxRewardPlacementResolver.Resolve(Reward(700, 1, false), 0, 0,
            full, full);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.InventoryFull, result.Outcome);
        Assert.Null(result.NewStack);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Resolve_NonStackableReward_NeverAttemptsAMerge_EvenIfASameIdStackExists()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(0, Filler)
            .SetItem(1, new ItemStack(900, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2));

        var result = BoxRewardPlacementResolver.Resolve(Reward(900, 1, false), 0, 0,
            page0, ImmutableDictionary<byte, ItemStack>.Empty);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, result.Outcome);
        Assert.Equal(2, result.Slot);
    }
}
