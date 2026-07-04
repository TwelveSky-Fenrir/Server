using Fenrir.Application.Game.Buffs;

namespace Fenrir.Application.Game.Tests.Buffs;

public class PlaytimeBuffResolverTests
{
    [Theory]
    [InlineData(1, 120)]
    [InlineData(2, 180)]
    [InlineData(3, 240)]
    [InlineData(4, 300)]
    [InlineData(5, 360)]
    public void ValidSort_AlwaysApplies_DueToHardResetQuirk(int sort, int expectedValue)
    {
        var result = PlaytimeBuffResolver.Resolve(sort);

        Assert.True(result.Applied);
        Assert.Equal(expectedValue, result.Value);
        Assert.Equal(expectedValue, result.NewStateTimeEffect);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void OutOfRangeSort_IsIgnored_NoDisconnectValueMinusOne(int sort)
    {
        var result = PlaytimeBuffResolver.Resolve(sort);

        Assert.False(result.Applied);
        Assert.Equal(-1, result.Value);
    }
}
