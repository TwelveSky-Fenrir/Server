using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

/// <summary>
///     Coverage for <c>SetItemQuantity</c> (<see cref="BoxRewardPlacementResolver.ResolveQuantity" />): a box
///     reward's slot quantity is decided purely by its sort -- stackable clamped 1..999, pet clamped 0..100,
///     everything else zeroed -- and the reported stackability is what the placement step then honors.
/// </summary>
public class BoxRewardQuantityTests
{
    [Theory]
    [InlineData(2, 1, 1)] // stackable, one -> 1
    [InlineData(2, 0, 1)] // stackable floor is 1, never 0
    [InlineData(2, 5000, 999)] // stackable ceiling is the 999 duplication cap
    [InlineData(99, 1, 1)] // sort 99 is stackable too (materials stacking)
    public void ResolveQuantity_StackableSort_ClampsToOneThroughDuplicationCap(byte sort, int rolled, int expected)
    {
        var result = BoxRewardPlacementResolver.ResolveQuantity(sort, rolled);

        Assert.True(result.IsStackable);
        Assert.Equal(expected, result.Quantity);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(150, 100)] // pet ceiling is 100
    public void ResolveQuantity_PetSort_ClampsToZeroThroughHundred_NotStackable(int rolled, int expected)
    {
        var result = BoxRewardPlacementResolver.ResolveQuantity(BoxRewardPlacementResolver.PetSort, rolled);

        Assert.False(result.IsStackable);
        Assert.Equal(expected, result.Quantity);
    }

    [Theory]
    [InlineData(4)] // weapon
    [InlineData(29)] // cape
    [InlineData(30)] // costume-ish
    public void ResolveQuantity_EveryOtherSort_IsZeroAndNotStackable(byte sort)
    {
        var result = BoxRewardPlacementResolver.ResolveQuantity(sort, 1);

        Assert.False(result.IsStackable);
        Assert.Equal(0, result.Quantity);
    }
}
