using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Consumables;

public class BoxRewardPlacementResolverVaultGateTests
{
    private static readonly ItemStack Filler = new(999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);

    private static BoxRewardPlacementResolver.ResolvedReward Reward(int itemId, int quantity, bool stackable)
    {
        return new BoxRewardPlacementResolver.ResolvedReward(itemId, quantity, stackable, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void SecondPageInaccessible_ExistingStackOnlyOnPage1_IsNotMerged_FallsToEmptySlotOnPage0()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Filler);
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(0, new ItemStack(500, 10, 0, 0, 0, 0, 0, 0, 0, 0, 2));

        var result = BoxRewardPlacementResolver.Resolve(Reward(500, 4, true), 0, 0, page0, page1,
            secondPageAccessible: false);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, result.Outcome);
        Assert.Equal(ContainerMatrix.InventoryPage0, result.Container);
        Assert.Equal(1, result.Slot);
    }

    [Fact]
    public void SecondPageInaccessible_Page0Full_ReportsInventoryFull_InsteadOfFallingThroughToPage1()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        ContainerMatrix.TryGetMaxSlot(ContainerMatrix.InventoryPage0, out var maxSlot);
        for (var slot = 0; slot <= maxSlot; slot++)
            builder[(byte)slot] = Filler;
        var fullPage0 = builder.ToImmutable();

        var result = BoxRewardPlacementResolver.Resolve(Reward(700, 1, false), 0, 0, fullPage0,
            ImmutableDictionary<byte, ItemStack>.Empty, secondPageAccessible: false);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.InventoryFull, result.Outcome);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void SecondPageAccessible_Page0Full_StillFallsThroughToPage1_MatchingDefaultBehavior()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        ContainerMatrix.TryGetMaxSlot(ContainerMatrix.InventoryPage0, out var maxSlot);
        for (var slot = 0; slot <= maxSlot; slot++)
            builder[(byte)slot] = Filler;
        var fullPage0 = builder.ToImmutable();

        var result = BoxRewardPlacementResolver.Resolve(Reward(700, 1, false), 0, 0, fullPage0,
            ImmutableDictionary<byte, ItemStack>.Empty, secondPageAccessible: true);

        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, result.Outcome);
        Assert.Equal(ContainerMatrix.InventoryPage1, result.Container);
    }
}
