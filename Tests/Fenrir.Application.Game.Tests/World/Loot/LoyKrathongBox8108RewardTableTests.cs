using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class LoyKrathongBox8108RewardTableTests
{
    private static readonly HashSet<int> AllKnownRewardIds =
    [
        1407, 1403, 1404, 826, 619,
        90786, 90787, 90788, 90789, 90790, 90791, 90792, 90793, 90794,
        1103, 1237, 1166, 578, 579, 1017, 1018, 1092, 1093, 698, 696, 695
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Roll_FixedBand_Returns1407(int roll)
    {
        var result = LoyKrathongBox8108RewardTable.Roll(0, new ScriptedRandom(roll));

        Assert.True(result.Success);
        Assert.Equal(1407, result.RewardItemId);
    }

    [Theory]
    [InlineData(0, 1403)]
    [InlineData(3, 1403)]
    [InlineData(4, 1404)]
    [InlineData(6, 1404)]
    public void Roll_SeventhsBand_FourVersusThreeSplit_MapsSubRollToAssumedId(int subRoll, int expectedRewardId)
    {
        var result = LoyKrathongBox8108RewardTable.Roll(0, new ScriptedRandom(6, subRoll));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_SeventhsBand_TopBoundary7_StillInBand()
    {
        var result = LoyKrathongBox8108RewardTable.Roll(0, new ScriptedRandom(7, 0));

        Assert.True(result.Success);
        Assert.Equal(1403, result.RewardItemId);
    }

    [Theory]
    [InlineData((byte)0, 90787, 0)]
    [InlineData((byte)0, 90786, 1)]
    [InlineData((byte)0, 90788, 2)]
    [InlineData((byte)1, 90789, 0)]
    [InlineData((byte)2, 90793, 0)]
    public void Roll_EpicBand_TribeKeyedUniformPool(byte previousTribe, int expectedRewardId, int withinPoolDraw)
    {
        var result = LoyKrathongBox8108RewardTable.Roll(previousTribe, new ScriptedRandom(8, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_EpicBand_TopBoundary9_StillInBand()
    {
        var result = LoyKrathongBox8108RewardTable.Roll(0, new ScriptedRandom(9, 0));

        Assert.True(result.Success);
        Assert.Equal(90787, result.RewardItemId);
    }

    [Fact]
    public void Roll_EpicBand_UnrecognizedTribe_FailsClosed_AlreadyLegacyFaithful()
    {
        var result = LoyKrathongBox8108RewardTable.Roll(99, new ScriptedRandom(8));

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
    }

    [Theory]
    [InlineData(0, 826)]
    [InlineData(1, 826)]
    [InlineData(2, 619)]
    [InlineData(5, 619)]
    public void Roll_SixthsBand_TwoVersusFourSplit_MapsSubRollToExpectedId(int subRoll, int expectedRewardId)
    {
        var result = LoyKrathongBox8108RewardTable.Roll(0, new ScriptedRandom(10, subRoll));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void Roll_SixthsBand_TopBoundary11_StillInBand()
    {
        var result = LoyKrathongBox8108RewardTable.Roll(0, new ScriptedRandom(11, 0));

        Assert.True(result.Success);
        Assert.Equal(826, result.RewardItemId);
    }

    [Theory]
    [InlineData(12, 0, 1103)]
    [InlineData(50, 7, 1092)]
    [InlineData(99, 11, 695)]
    public void Roll_CommonPoolBand_UniformDraw(int roll, int withinPoolDraw, int expectedRewardId)
    {
        var result = LoyKrathongBox8108RewardTable.Roll(0, new ScriptedRandom(roll, withinPoolDraw));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
    }

    [Fact]
    public void CommonPoolIds_AreExactlyTheTwelveCitedIds()
    {
        int[] expected = [1103, 1237, 1166, 578, 579, 1017, 1018, 1092, 1093, 698, 696, 695];
        Assert.Equal(expected, LoyKrathongBox8108RewardTable.CommonPoolIds.ToArray());
    }

    [Fact]
    public void EpicIdsByPreviousTribe_AreExactlyTheThreeCitedTriplesPerTribe()
    {
        Assert.Equal([90787, 90786, 90788], LoyKrathongBox8108RewardTable.EpicIdsByPreviousTribe[0].ToArray());
        Assert.Equal([90789, 90790, 90791], LoyKrathongBox8108RewardTable.EpicIdsByPreviousTribe[1].ToArray());
        Assert.Equal([90793, 90792, 90794], LoyKrathongBox8108RewardTable.EpicIdsByPreviousTribe[2].ToArray());
    }

    [Fact]
    public void Roll_NeverAcceptsAnyClientSuppliedValue_SignatureHasNoSuchParameter()
    {
        var method = typeof(LoyKrathongBox8108RewardTable).GetMethod(nameof(LoyKrathongBox8108RewardTable.Roll));

        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(byte), parameters[0].ParameterType);
        Assert.Equal(typeof(Random), parameters[1].ParameterType);
    }

    [Fact]
    public void Roll_AcrossManyRealRandomDraws_EveryRewardIsAlwaysWithinTheKnownTable()
    {
        var random = new Random(20260711);

        foreach (var tribe in new byte[] { 0, 1, 2 })
            for (var i = 0; i < 5_000; i++)
            {
                var result = LoyKrathongBox8108RewardTable.Roll(tribe, random);

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
