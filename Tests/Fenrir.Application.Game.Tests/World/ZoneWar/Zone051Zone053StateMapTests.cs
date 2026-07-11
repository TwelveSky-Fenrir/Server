using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone051Zone053StateMapTests
{
    [Theory]
    [InlineData(11, 1)]
    [InlineData(12, 2)]
    [InlineData(13, 3)]
    [InlineData(14, 5)]
    [InlineData(15, 5)]
    [InlineData(16, 4)]
    [InlineData(17, 5)]
    [InlineData(18, 0)]
    public void Zone051_Selectors11Through18_MapToTheExactState(int selector, int expected)
    {
        Assert.True(Zone051Zone053StateMap.TryMapZone051(selector, out var state));
        Assert.Equal(expected, state);
    }

    [Fact]
    public void Zone051_Selector10_IsTheBlockOpeningNoOp_DoesNotMap()
    {
        Assert.False(Zone051Zone053StateMap.TryMapZone051(10, out _));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(19)]
    [InlineData(0)]
    public void Zone051_OutOfRangeSelectors_DoNotMap(int selector)
    {
        Assert.False(Zone051Zone053StateMap.TryMapZone051(selector, out _));
    }

    [Fact]
    public void Zone051_EverySelectorIn11Through18_Maps_AndNothingElseDoes()
    {
        for (var selector = 0; selector < 40; selector++)
        {
            var mapped = Zone051Zone053StateMap.TryMapZone051(selector, out _);
            Assert.Equal(selector is >= 11 and <= 18, mapped);
        }
    }

    [Theory]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(22, 3)]
    [InlineData(23, 5)]
    [InlineData(24, 5)]
    [InlineData(28, 4)]
    [InlineData(29, 5)]
    [InlineData(30, 0)]
    public void Zone053_Selectors20Through30_MapToTheExactState(int selector, int expected)
    {
        Assert.True(Zone051Zone053StateMap.TryMapZone053(selector, out var state));
        Assert.Equal(expected, state);
    }

    [Fact]
    public void Zone053_Selector19_IsDeadCodeInEveryShippedBuild_DoesNotMap()
    {
        Assert.False(Zone051Zone053StateMap.TryMapZone053(19, out _));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(27)]
    public void Zone053_BareNoOpSelectors_DoNotMap(int selector)
    {
        Assert.False(Zone051Zone053StateMap.TryMapZone053(selector, out _));
    }

    [Theory]
    [InlineData(18)]
    [InlineData(31)]
    [InlineData(0)]
    public void Zone053_OutOfRangeSelectors_DoNotMap(int selector)
    {
        Assert.False(Zone051Zone053StateMap.TryMapZone053(selector, out _));
    }

    [Fact]
    public void Zone053_EverySelectorIn20Through30ExceptTheNoOps_Maps_AndNothingElseDoes()
    {
        for (var selector = 0; selector < 40; selector++)
        {
            var mapped = Zone051Zone053StateMap.TryMapZone053(selector, out _);
            var expectMapped = selector is 20 or 21 or 22 or 23 or 24 or 28 or 29 or 30;
            Assert.Equal(expectMapped, mapped);
        }
    }
}
