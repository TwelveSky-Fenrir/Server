using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class BoxRewardQuantityTests
{
    [Theory]
    [InlineData(2, 1, 1)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 5000, 999)]
    [InlineData(99, 1, 1)]
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
    [InlineData(150, 100)]
    public void ResolveQuantity_PetSort_ClampsToZeroThroughHundred_NotStackable(int rolled, int expected)
    {
        var result = BoxRewardPlacementResolver.ResolveQuantity(BoxRewardPlacementResolver.PetSort, rolled);

        Assert.False(result.IsStackable);
        Assert.Equal(expected, result.Quantity);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(29)]
    [InlineData(30)]
    public void ResolveQuantity_EveryOtherSort_IsZeroAndNotStackable(byte sort)
    {
        var result = BoxRewardPlacementResolver.ResolveQuantity(sort, 1);

        Assert.False(result.IsStackable);
        Assert.Equal(0, result.Quantity);
    }
}
