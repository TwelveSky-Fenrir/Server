using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Fishing;

namespace Fenrir.Application.Game.Tests.Fishing;

public class FishingRewardResolverTests
{
    [Fact]
    public void RollBite_ZeroDraw_IsABite()
    {
        Assert.True(FishingRewardResolver.RollBite(new ScriptedRandomSource(0)));
    }

    [Fact]
    public void RollBite_NonZeroDraw_IsAMiss()
    {
        Assert.False(FishingRewardResolver.RollBite(new ScriptedRandomSource(1)));
    }

    [Fact]
    public void RollRewardItem_Roll0AndSubRoll0_IsMountKoi()
    {
        Assert.Equal(583, FishingRewardResolver.RollRewardItem(new ScriptedRandomSource(0, 0)));
    }

    [Fact]
    public void RollRewardItem_Roll0AndSubRoll1_IsPetKoi()
    {
        Assert.Equal(584, FishingRewardResolver.RollRewardItem(new ScriptedRandomSource(0, 1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RollRewardItem_Roll1To3_IsElixirKoi(int roll)
    {
        Assert.Equal(582, FishingRewardResolver.RollRewardItem(new ScriptedRandomSource(roll)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(14)]
    public void RollRewardItem_Roll4To14_IsTicketKoi(int roll)
    {
        Assert.Equal(586, FishingRewardResolver.RollRewardItem(new ScriptedRandomSource(roll)));
    }

    private sealed class ScriptedRandomSource(params int[] sequence) : IRandomSource
    {
        private int _index;

        public int NextInt32(int exclusiveUpperBound)
        {
            return sequence[_index++] % exclusiveUpperBound;
        }
    }
}
