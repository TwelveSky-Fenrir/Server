using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class ChestBox720RewardTableTests
{
    private static readonly HashSet<int> AllKnownRewardIds =
    [
        15157, 15267, 15135, 15179, 15223, 15245, 15289,
        35157, 35267, 35135, 35179, 35223, 35245, 35289,
        55157, 55267, 55135, 55179, 55223, 55245, 55289,
        1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326,
        1449, 1072,
        801, 802, 803, 804, 805, 806,
        1437, 1178, 698, 1166
    ];

    [Theory]
    [InlineData((byte)0, 0, 15157)]
    [InlineData((byte)0, 6, 15289)]
    [InlineData((byte)1, 0, 35157)]
    [InlineData((byte)2, 6, 55289)]
    public void Roll_TribePoolBranch_BaseIdPlusTribeOffset(byte previousTribe, int withinPoolDraw,
        int expectedRewardId)
    {
        var result = ChestBox720RewardTable.Roll(previousTribe, new ScriptedRandom(0, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_TribePoolBranch_TopBoundary14_StillInBranch()
    {
        var result = ChestBox720RewardTable.Roll(0, new ScriptedRandom(14, 0));

        Assert.True(result.Success);
        Assert.Equal(15157, result.RewardItemId);
    }

    [Fact]
    public void Roll_TribePoolBranch_UnrecognizedTribe_FailsClosed_ConsumesOnlyOneDraw()
    {
        var result = ChestBox720RewardTable.Roll(99, new ScriptedRandom(0));

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
    }

    [Theory]
    [InlineData(0, 1301)]
    [InlineData(1, 1302)]
    [InlineData(2, 1303)]
    [InlineData(3, 1313)]
    [InlineData(4, 1317)]
    [InlineData(5, 1320)]
    [InlineData(6, 1323)]
    [InlineData(7, 1326)]
    public void Roll_EightyFivePercentBranch_AnimalSlot_UniformDrawMapsToExpectedTierOneAnimalId(int withinPoolDraw,
        int expectedRewardId)
    {
        var result = ChestBox720RewardTable.Roll(0, new ScriptedRandom(15, 0, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_EightyFivePercentBranch_AnimalSlot_UnrecognizedTribe_StillSucceeds()
    {
        var result = ChestBox720RewardTable.Roll(99, new ScriptedRandom(50, 0, 3));

        Assert.True(result.Success);
        Assert.Equal(1313, result.RewardItemId);
    }

    [Theory]
    [InlineData(0, 801)]
    [InlineData(1, 802)]
    [InlineData(2, 803)]
    [InlineData(3, 804)]
    [InlineData(4, 805)]
    [InlineData(5, 806)]
    public void Roll_EightyFivePercentBranch_ElixirPlusSlot_UniformDrawMapsToExpectedId(int withinPoolDraw,
        int expectedRewardId)
    {
        var result = ChestBox720RewardTable.Roll(0, new ScriptedRandom(50, 3, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Theory]
    [InlineData(1, 1449)]
    [InlineData(2, 1072)]
    [InlineData(4, 1437)]
    [InlineData(5, 1178)]
    [InlineData(6, 698)]
    [InlineData(7, 1166)]
    public void Roll_EightyFivePercentBranch_FixedIdSlots_ReturnsExpectedId_ConsumingExactlyTwoDraws(int slotDraw,
        int expectedRewardId)
    {
        var result = ChestBox720RewardTable.Roll(0, new ScriptedRandom(50, slotDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_EightyFivePercentBranch_TopBoundary99_StillInBranch()
    {
        var result = ChestBox720RewardTable.Roll(0, new ScriptedRandom(99, 7));

        Assert.True(result.Success);
        Assert.Equal(1166, result.RewardItemId);
    }

    [Fact]
    public void TribePoolBaseIds_AreExactlyTheSevenCitedIds()
    {
        int[] expected = [15157, 15267, 15135, 15179, 15223, 15245, 15289];
        Assert.Equal(expected, ChestBox720RewardTable.TribePoolBaseIds.ToArray());
    }

    [Fact]
    public void TribeOffsetByPreviousTribe_MatchesTheCitedOffsets()
    {
        Assert.Equal(0, ChestBox720RewardTable.TribeOffsetByPreviousTribe[0]);
        Assert.Equal(20000, ChestBox720RewardTable.TribeOffsetByPreviousTribe[1]);
        Assert.Equal(40000, ChestBox720RewardTable.TribeOffsetByPreviousTribe[2]);
    }

    [Fact]
    public void AnimalPoolIds_AreExactlyTheEightCitedTierOneAnimalIds()
    {
        int[] expected = [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326];
        Assert.Equal(expected, ChestBox720RewardTable.AnimalPoolIds.ToArray());
    }

    [Fact]
    public void ElixirPlusPoolIds_AreExactlyTheSixCitedIds()
    {
        int[] expected = [801, 802, 803, 804, 805, 806];
        Assert.Equal(expected, ChestBox720RewardTable.ElixirPlusPoolIds.ToArray());
    }

        [Fact]
    public void Roll_AcrossManyRealRandomDraws_EveryRewardIsAlwaysWithinTheKnownTable()
    {
        var random = new Random(20260711);

        foreach (byte tribe in new byte[] { 0, 1, 2 })
            for (var i = 0; i < 5_000; i++)
            {
                var result = ChestBox720RewardTable.Roll(tribe, random);

                Assert.True(result.Success);
                Assert.Contains(result.RewardItemId, AllKnownRewardIds);
            }
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
