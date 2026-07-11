using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class WingLuckyBox8005RewardTableTests
{
    [Theory]
    [InlineData((byte)0, 213)]
    [InlineData((byte)1, 214)]
    [InlineData((byte)2, 215)]
    public void Roll_BlueDragonWingsBand_TribeKeyedId(byte previousTribe, int expectedRewardId)
    {
        var result = WingLuckyBox8005RewardTable.Roll(previousTribe, new ScriptedRandom(0));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_BlueDragonWingsBand_TopBoundary79_StillInBand()
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(79));

        Assert.True(result.Success);
        Assert.Equal(213, result.RewardItemId);
    }

    [Fact]
    public void Roll_BlueDragonWingsBand_UnrecognizedTribe_FailsClosed_AlreadyLegacyFaithful()
    {
        var result = WingLuckyBox8005RewardTable.Roll(99, new ScriptedRandom(0));

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
    }

    [Theory]
    [InlineData((byte)0, 216)]
    [InlineData((byte)1, 217)]
    [InlineData((byte)2, 218)]
    public void Roll_ArchangelWingsBand_TribeKeyedId(byte previousTribe, int expectedRewardId)
    {
        var result = WingLuckyBox8005RewardTable.Roll(previousTribe, new ScriptedRandom(80));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_ArchangelWingsBand_TopBoundary129_StillInBand()
    {
        var result = WingLuckyBox8005RewardTable.Roll(2, new ScriptedRandom(129));

        Assert.True(result.Success);
        Assert.Equal(218, result.RewardItemId);
    }

    [Fact]
    public void Roll_ArchangelWingsBand_UnrecognizedTribe_FailsClosed()
    {
        var result = WingLuckyBox8005RewardTable.Roll(99, new ScriptedRandom(80));

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Roll_SecondRollFixedBand_Returns2477(int secondRoll)
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, secondRoll));

        Assert.True(result.Success);
        Assert.Equal(2477, result.RewardItemId);
    }

    [Theory]
    [InlineData((byte)0, 201)]
    [InlineData((byte)1, 202)]
    [InlineData((byte)2, 203)]
    public void Roll_SecondRollTribeBand_TribeKeyedId(byte previousTribe, int expectedRewardId)
    {
        var result = WingLuckyBox8005RewardTable.Roll(previousTribe, new ScriptedRandom(130, 6));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_SecondRollTribeBand_TopBoundary60_StillInBand()
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, 60));

        Assert.True(result.Success);
        Assert.Equal(201, result.RewardItemId);
    }

    [Fact]
    public void Roll_SecondRollTribeBand_UnrecognizedTribe_FailsClosed_AlreadyLegacyFaithful()
    {
        var result = WingLuckyBox8005RewardTable.Roll(99, new ScriptedRandom(130, 6));

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
    }

    [Theory]
    [InlineData(0, 2397)]
    [InlineData(5, 698)]
    public void Roll_MiscPool_UniformDraw(int withinPoolDraw, int expectedRewardId)
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, 61, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_MiscPool_TopBoundary100_StillInBand()
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, 100, 0));

        Assert.True(result.Success);
        Assert.Equal(2397, result.RewardItemId);
    }

    [Theory]
    [InlineData(0, 506)]
    [InlineData(5, 579)]
    public void Roll_ElixirPool_UniformDraw(int withinPoolDraw, int expectedRewardId)
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, 101, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_ElixirPool_TopBoundary160_StillInBand()
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, 160, 0));

        Assert.True(result.Success);
        Assert.Equal(506, result.RewardItemId);
    }

    [Theory]
    [InlineData(0, 1166)]
    [InlineData(5, 1237)]
    public void Roll_CharmPool_UniformDraw(int withinPoolDraw, int expectedRewardId)
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, 161, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_CharmPool_TopBoundary199_StillInBand()
    {
        var result = WingLuckyBox8005RewardTable.Roll(0, new ScriptedRandom(130, 199, 5));

        Assert.True(result.Success);
        Assert.Equal(1237, result.RewardItemId);
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
