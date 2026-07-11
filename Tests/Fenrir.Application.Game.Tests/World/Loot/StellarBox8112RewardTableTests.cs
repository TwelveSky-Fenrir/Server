using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class StellarBox8112RewardTableTests
{
    private static readonly BoxRewardSpec Spec = StellarBox8112RewardTable.Spec;

    [Fact]
    public void Spec_IsWeighted_ForBoxId8112_WithNoRental()
    {
        Assert.Equal(8112, Spec.BoxId);
        Assert.Equal(BoxRewardKind.Weighted, Spec.Kind);
        Assert.Equal(0, Spec.RentalDays);
    }

    [Fact]
    public void Spec_HasExactlySevenBands_InAscendingCutoffOrder_SummingToOneThousand()
    {
        Assert.Equal(7, Spec.WeightedRewards.Length);

        int[] expectedIds = [93500, 93501, 93502, 93503, 93504, 93505, 93506];
        int[] expectedWeights = [350, 200, 150, 120, 90, 60, 30];

        for (var i = 0; i < 7; i++)
        {
            Assert.Equal(expectedIds[i], Spec.WeightedRewards[i].ItemId);
            Assert.Equal(expectedWeights[i], Spec.WeightedRewards[i].Weight);
        }

        Assert.Equal(1000, expectedWeights.Sum());
    }

    [Theory]
    [InlineData(0, 93500)]
    [InlineData(349, 93500)]
    [InlineData(350, 93501)]
    [InlineData(549, 93501)]
    [InlineData(550, 93502)]
    [InlineData(699, 93502)]
    [InlineData(700, 93503)]
    [InlineData(819, 93503)]
    [InlineData(820, 93504)]
    [InlineData(909, 93504)]
    [InlineData(910, 93505)]
    [InlineData(969, 93505)]
    [InlineData(970, 93506)]
    [InlineData(999, 93506)]
    public void RollRewardId_AtEveryBandBoundary_ReturnsTheExpectedGrade(int roll, int expectedRewardId)
    {
        var random = new ScriptedRandom(roll);

        Assert.Equal(expectedRewardId, Spec.RollRewardId(random));
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
