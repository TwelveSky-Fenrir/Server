using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class WarlordChestRewardTableTests
{
    [Fact]
    public void RarePools_ND_AreExactlyTheNineCitedIds()
    {
        int[] expected = [87013, 87014, 87015, 87016, 87017, 87018, 87019, 87020, 87008];
        Assert.Equal(expected, WarlordChestRewardTable.RarePoolsByPreviousTribe[0].ToArray());
    }

    [Fact]
    public void RarePools_RS_AreExactlyTheNineCitedIds()
    {
        int[] expected = [87034, 87035, 87036, 87037, 87038, 87039, 87040, 87041, 87029];
        Assert.Equal(expected, WarlordChestRewardTable.RarePoolsByPreviousTribe[1].ToArray());
    }

    [Fact]
    public void RarePools_GT_AreExactlyTheNineCitedIds()
    {
        int[] expected = [87055, 87056, 87057, 87058, 87059, 87060, 87061, 87062, 87050];
        Assert.Equal(expected, WarlordChestRewardTable.RarePoolsByPreviousTribe[2].ToArray());
    }

    [Fact]
    public void ElitePools_ND_AreExactlyTheFourteenLiveContiguousIds()
    {
        int[] expected =
        [
            87071, 87072, 87073, 87074, 87075, 87076,
            87077, 87078, 87079, 87080, 87081, 87082, 87083, 87084
        ];
        Assert.Equal(expected, WarlordChestRewardTable.ElitePoolsByPreviousTribe[0].ToArray());
    }

    [Fact]
    public void ElitePools_RS_AreExactlyTheFourteenLiveContiguousIds()
    {
        int[] expected =
        [
            87093, 87094, 87095, 87096, 87097, 87098,
            87099, 87100, 87101, 87102, 87103, 87104, 87105, 87106
        ];
        Assert.Equal(expected, WarlordChestRewardTable.ElitePoolsByPreviousTribe[1].ToArray());
    }

    [Fact]
    public void ElitePools_GT_AreExactlyTheFourteenLiveContiguousIds()
    {
        int[] expected =
        [
            87115, 87116, 87117, 87118, 87119, 87120,
            87121, 87122, 87123, 87124, 87125, 87126, 87127, 87128
        ];
        Assert.Equal(expected, WarlordChestRewardTable.ElitePoolsByPreviousTribe[2].ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ElitePools_HaveExactlyFourteenEntries_NoDeadIdsIncluded(byte previousTribe)
    {
        var pool = WarlordChestRewardTable.ElitePoolsByPreviousTribe[previousTribe];

        Assert.Equal(14, pool.Length);
        Assert.Equal(pool.Length, pool.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RarePools_HaveExactlyNineEntries_NoDuplicates(byte previousTribe)
    {
        var pool = WarlordChestRewardTable.RarePoolsByPreviousTribe[previousTribe];

        Assert.Equal(9, pool.Length);
        Assert.Equal(pool.Length, pool.Distinct().Count());
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(13, true)]
    [InlineData(0, false)]
    public void MeetsLevelGate_ChecksAgainstTwelveInclusive(short level2, bool expected)
    {
        Assert.Equal(expected, WarlordChestRewardTable.MeetsLevelGate(level2));
    }

    [Theory]
    [InlineData(0, 0, 87013)]
    [InlineData(0, 8, 87008)]
    [InlineData(1, 0, 87034)]
    [InlineData(2, 8, 87050)]
    public void TryRollReward_EarthChest_UniformDraw_MapsIndexToExpectedReward(byte previousTribe, int drawIndex,
        int expectedRewardId)
    {
        var rolled = WarlordChestRewardTable.TryRollReward(WarlordChestRewardTable.EarthChestBoxItemId,
            previousTribe, new ScriptedRandom(drawIndex), out var rewardItemId);

        Assert.True(rolled);
        Assert.Equal(expectedRewardId, rewardItemId);
    }

    [Theory]
    [InlineData(0, 0, 87071)]
    [InlineData(0, 13, 87084)]
    [InlineData(1, 0, 87093)]
    [InlineData(2, 13, 87128)]
    public void TryRollReward_SkyChest_UniformDraw_MapsIndexToExpectedReward(byte previousTribe, int drawIndex,
        int expectedRewardId)
    {
        var rolled = WarlordChestRewardTable.TryRollReward(WarlordChestRewardTable.SkyChestBoxItemId,
            previousTribe, new ScriptedRandom(drawIndex), out var rewardItemId);

        Assert.True(rolled);
        Assert.Equal(expectedRewardId, rewardItemId);
    }

    [Fact]
    public void TryRollReward_UnrecognizedBoxItemId_ReturnsFalse()
    {
        var rolled = WarlordChestRewardTable.TryRollReward(999999, 0, new ScriptedRandom(0), out var rewardItemId);

        Assert.False(rolled);
        Assert.Equal(0, rewardItemId);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public void TryRollReward_UnrecognizedTribe_ReturnsFalse_ForBothChests(byte previousTribe)
    {
        Assert.False(WarlordChestRewardTable.TryRollReward(WarlordChestRewardTable.EarthChestBoxItemId,
            previousTribe, new ScriptedRandom(0), out _));
        Assert.False(WarlordChestRewardTable.TryRollReward(WarlordChestRewardTable.SkyChestBoxItemId,
            previousTribe, new ScriptedRandom(0), out _));
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
