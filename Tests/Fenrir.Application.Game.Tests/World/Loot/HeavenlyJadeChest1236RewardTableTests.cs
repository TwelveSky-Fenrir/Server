using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class HeavenlyJadeChest1236RewardTableTests
{
    [Theory]
    [InlineData((byte)0, 2307)]
    [InlineData((byte)1, 2308)]
    [InlineData((byte)2, 2309)]
    public void Roll_ZeroBranch_CatHairBandSubBranch_MapsRecognizedTribeToExpectedId(byte previousTribe,
        int expectedRewardId)
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(previousTribe, new ScriptedRandom(0, 0));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_ZeroBranch_CatHairBandSubBranch_UnrecognizedTribe_FailsClosed()
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(99, new ScriptedRandom(0, 0));

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
    }

    [Fact]
    public void Roll_ZeroBranch_AlternateSubBranch_Returns1321_RegardlessOfTribe()
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(99, new ScriptedRandom(0, 1));

        Assert.True(result.Success);
        Assert.Equal(1321, result.RewardItemId);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(9)]
    public void Roll_ZeroBranch_FallbackSubBranch_Returns1324_ForTheEightOfTenRemainder(int innerDraw)
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(0, new ScriptedRandom(0, innerDraw));

        Assert.True(result.Success);
        Assert.Equal(1324, result.RewardItemId);
    }

    [Theory]
    [InlineData(1, 0, 1007)]
    [InlineData(30, 1, 1008)]
    public void Roll_OneToThirtyBranch_CoinFlipMapsToExpectedFixedId(int outerRoll, int coin, int expectedRewardId)
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(0, new ScriptedRandom(outerRoll, coin));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Theory]
    [InlineData((byte)0, 126)]
    [InlineData((byte)1, 129)]
    [InlineData((byte)2, 132)]
    public void Roll_ThirtyOneToFiftyBranch_TribeKeyedFixedId(byte previousTribe, int expectedRewardId)
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(previousTribe, new ScriptedRandom(31));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_ThirtyOneToFiftyBranch_UnrecognizedTribe_FailsClosed()
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(200, new ScriptedRandom(50));

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
    }

    [Theory]
    [InlineData(51, 0, 601)]
    [InlineData(75, 1, 602)]
    [InlineData(100, 2, 2249)]
    public void Roll_FiftyOneToHundredBranch_ThirdsMapToExpectedFixedId(int outerRoll, int third,
        int expectedRewardId)
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(0, new ScriptedRandom(outerRoll, third));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Theory]
    [InlineData(101, 0, 506)]
    [InlineData(400, 2, 509)]
    [InlineData(699, 4, 579)]
    public void Roll_ElixirBranch_UniformDrawMapsToExpectedReward(int outerRoll, int withinPoolDraw,
        int expectedRewardId)
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(0, new ScriptedRandom(outerRoll, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Theory]
    [InlineData(700)]
    [InlineData(999)]
    public void Roll_SevenHundredBranch_Returns1045_ConsumingOnlyOneDraw(int outerRoll)
    {
        var result = HeavenlyJadeChest1236RewardTable.Roll(0, new ScriptedRandom(outerRoll));

        Assert.True(result.Success);
        Assert.Equal(1045, result.RewardItemId);
    }

        private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length)
                throw new InvalidOperationException(
                    "ScriptedRandom exhausted: the code drew more values than scripted.");

            return values[_index++];
        }
    }
}
