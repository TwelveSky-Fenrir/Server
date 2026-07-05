using Fenrir.Application.Game.Domain.Buffs;

namespace Fenrir.Application.Game.Tests.Buffs;

public class RankBuffResolverTests
{
    [Fact]
    public void Sort1_WithOneStone_Succeeds()
    {
        var result = RankBuffResolver.Resolve(1, 1);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Sort1_WithZeroStones_Rejected()
    {
        var result = RankBuffResolver.Resolve(1, 0);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(6, 3)]
    [InlineData(7, 3)]
    public void HigherSorts_RequireMoreStonesThanTypicalDefault_Rejected(int sort, int stoneCount)
    {
        var result = RankBuffResolver.Resolve(sort, stoneCount);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    [InlineData(7, 4)]
    public void HigherSorts_WithExactRequiredStoneCount_Succeeds(int sort, int stoneCount)
    {
        var result = RankBuffResolver.Resolve(sort, stoneCount);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void OutOfRangeSort_Rejected(int sort)
    {
        var result = RankBuffResolver.Resolve(sort, 100);

        Assert.False(result.Succeeded);
    }
}
