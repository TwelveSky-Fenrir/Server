using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Covers the C10-remaining-box-pools reward-table DATA and roll primitive for item 8108 (Loy Krathong Box)
///     -- <see cref="LoyKrathongBox8108RewardTable" />. <b>Security-focused</b>: the legacy single-open code
///     path for this item contains a confirmed arbitrary-item-grant exploit (see that type's own SECURITY
///     remarks) that Fenrir does not reproduce -- <see cref="Roll_NeverAcceptsAnyClientSuppliedValue_SignatureHasNoSuchParameter" />
///     and <see cref="Roll_AcrossManyRealRandomDraws_EveryRewardIsAlwaysWithinTheKnownTable" /> below are the
///     direct proof of that hardening for this table; the
///     <c>LoyKrathongBox8108_SECURITY_ClientSuppliedExtremeValue_NeverInfluencesGrantedReward_OnlyClampsBulkCount</c>
///     test in <c>Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes.LootBoxUseItemHandlerTribeKeyedDispatchTests</c>
///     proves the same property end-to-end through the handler.
/// </summary>
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
        // Roll 6 or 7 enters this band; sub-roll of sevenths decides 1403 (4-in-7) vs 1404 (3-in-7). The
        // direction (which id gets the 4-in-7 share) is this table's own flagged, non-citation-confirmed
        // assumption -- see LoyKrathongBox8108RewardTable's own remarks. This test locks in that assumption so
        // any accidental flip is caught, not so it can never be revisited once a fresh citation resolves it.
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
        // Roll 10 or 11 enters this band; the contract IS explicit about direction here (826 on a third,
        // 619 on two-thirds), unlike the sevenths band above.
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

    /// <summary>
    ///     SECURITY: <see cref="LoyKrathongBox8108RewardTable.Roll" /> has exactly two parameters --
    ///     <c>previousTribe</c> and <c>random</c> -- and no way whatsoever to receive a client-supplied reward
    ///     id or item value. This is the structural guarantee that closes the legacy single-open exploit at the
    ///     API surface itself, not merely by convention.
    /// </summary>
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

    /// <summary>
    ///     SECURITY: across many real (non-scripted) random draws and every recognized tribe, every reward this
    ///     table ever produces is a member of its own known, bounded id set -- never an arbitrary value. This is
    ///     the data-level counterpart to the legacy single-open exploit's own defect (an unbounded id reachable
    ///     32% of the time) -- here that surface simply cannot exist.
    /// </summary>
    [Fact]
    public void Roll_AcrossManyRealRandomDraws_EveryRewardIsAlwaysWithinTheKnownTable()
    {
        var random = new Random(20260711);

        foreach (byte tribe in new byte[] { 0, 1, 2 })
            for (var i = 0; i < 5_000; i++)
            {
                var result = LoyKrathongBox8108RewardTable.Roll(tribe, random);

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
