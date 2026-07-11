using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Covers the C10-remaining-box-pools reward-table DATA and roll primitive for item 720 (Chest / Reward
///     Box) -- <see cref="ChestBox720RewardTable" />. Both branches are now fully recovered: the tribe-keyed
///     &lt;15% branch, and the 85% branch's 8 equally-likely slots (including the <c>GetRandomAnimal5()</c>
///     tier-1 animal pool a fresh legacy re-verification pass located).
/// </summary>
public class ChestBox720RewardTableTests
{
    private static readonly HashSet<int> AllKnownRewardIds =
    [
        // Tribe-keyed <15% branch: 7 base ids x 3 tribe offsets (0/+20000/+40000).
        15157, 15267, 15135, 15179, 15223, 15245, 15289,
        35157, 35267, 35135, 35179, 35223, 35245, 35289,
        55157, 55267, 55135, 55179, 55223, 55245, 55289,
        // 85% branch: 8 slots, 2 of them composite pools.
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
        // outer draw < 15 -> tribe pool branch; within-pool draw picks the base id, then the tribe offset is added.
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
        // Hardening choice: the single-path copy would otherwise attempt a modulo-by-zero on an empty pool;
        // Fenrir always fails closed instead, the bulk-path's own shape. Only ONE scripted value: ScriptedRandom
        // throws if a second (within-pool) draw were consumed after the tribe lookup fails.
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
        // outer=15 (bottom boundary of the 85% branch); slot draw 0 -> the GetRandomAnimal5 slot (slot 1 of 8).
        var result = ChestBox720RewardTable.Roll(0, new ScriptedRandom(15, 0, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_EightyFivePercentBranch_AnimalSlot_UnrecognizedTribe_StillSucceeds()
    {
        // The 85% branch has no tribe dependency at all -- an unrecognized tribe never fails it.
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
        // slot draw 3 -> the GetRandomElixirPlus slot (slot 4 of 8).
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
        // Only two scripted values: ScriptedRandom throws if a third draw were consumed for a non-composite slot.
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

    /// <summary>
    ///     Across many real (non-scripted) random draws and every recognized tribe, every reward this table ever
    ///     produces is a member of its own known, bounded id set -- covering both branches and all 8+2 composite
    ///     possibilities of the 85% branch, not just the deterministic boundary cases above.
    /// </summary>
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

    /// <summary>Returns queued draws in request order; throws if the code draws more than were scripted.</summary>
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
