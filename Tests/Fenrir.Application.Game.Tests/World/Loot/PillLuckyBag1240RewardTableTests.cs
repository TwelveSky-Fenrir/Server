using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class PillLuckyBag1240RewardTableTests
{
    private static readonly BoxRewardSpec Spec = PillLuckyBag1240RewardTable.Spec;

    [Fact]
    public void Spec_IsUniform_ForBox1240_WithNoRental()
    {
        Assert.Equal(1240, Spec.BoxId);
        Assert.Equal(BoxRewardKind.Uniform, Spec.Kind);
        Assert.Equal(0, Spec.RentalDays);
    }

    [Fact]
    public void RewardItemIds_AreExactlyTheFiveCitedIds_507Excluded()
    {
        int[] expected = [506, 508, 509, 578, 579];
        Assert.Equal(expected, PillLuckyBag1240RewardTable.RewardItemIds.ToArray());
        Assert.DoesNotContain(507, PillLuckyBag1240RewardTable.RewardItemIds);
    }

    [Theory]
    [InlineData(0, 506)]
    [InlineData(1, 508)]
    [InlineData(2, 509)]
    [InlineData(3, 578)]
    [InlineData(4, 579)]
    public void RollRewardId_UniformDraw_MapsIndexToExpectedReward(int drawIndex, int expectedRewardId)
    {
        Assert.Equal(expectedRewardId, Spec.RollRewardId(new ScriptedRandom(drawIndex)));
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
