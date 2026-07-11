using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class CostumeChest76543RewardTableTests
{
    [Theory]
    [InlineData(0, 76524)]
    [InlineData(1, 76525)]
    [InlineData(2, 76526)]
    public void TryResolveRewardId_RecognizedTribe_ReturnsTheDeterministicRewardId(byte previousTribe,
        int expectedRewardId)
    {
        var resolved = CostumeChest76543RewardTable.TryResolveRewardId(previousTribe, out var rewardId);

        Assert.True(resolved);
        Assert.Equal(expectedRewardId, rewardId);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(255)]
    public void TryResolveRewardId_UnrecognizedTribe_ReturnsFalse(byte previousTribe)
    {
        var resolved = CostumeChest76543RewardTable.TryResolveRewardId(previousTribe, out var rewardId);

        Assert.False(resolved);
        Assert.Equal(0, rewardId);
    }

    [Fact]
    public void RollEnchantGrade_AtLowerBoundDraw_ReturnsThirty()
    {
        var grade = CostumeChest76543RewardTable.RollEnchantGrade(new FixedRandom(30));

        Assert.Equal(30, grade);
    }

    [Fact]
    public void RollEnchantGrade_AtUpperBoundDraw_ReturnsSixty()
    {
        var grade = CostumeChest76543RewardTable.RollEnchantGrade(new FixedRandom(60));

        Assert.Equal(60, grade);
    }

    [Fact]
    public void RollEnchantGrade_DrawsWithInclusiveThirtyToSixtyRange()
    {
        var captured = new CapturingRandom();

        CostumeChest76543RewardTable.RollEnchantGrade(captured);

        Assert.Equal(30, captured.LastMinValue);
        Assert.Equal(61, captured.LastMaxValue);
    }

    [Theory]
    [InlineData(0, 76524)]
    [InlineData(1, 76525)]
    [InlineData(2, 76526)]
    public void Roll_RecognizedTribe_ReturnsSuccessWithRewardIdAndGrade(byte previousTribe, int expectedRewardId)
    {
        var result = CostumeChest76543RewardTable.Roll(previousTribe, new FixedRandom(45));

        Assert.True(result.Success);
        Assert.Equal(expectedRewardId, result.RewardItemId);
        Assert.Equal(45, result.EnchantGrade);
    }

    [Fact]
    public void Roll_UnrecognizedTribe_ReturnsFailure_AndDoesNotSpendARandomDraw()
    {
        var result = CostumeChest76543RewardTable.Roll(99, new NoDrawRandom());

        Assert.False(result.Success);
        Assert.Equal(0, result.RewardItemId);
        Assert.Equal(0, result.EnchantGrade);
    }

    [Fact]
    public void RentalDays_IsThreeDays()
    {
        Assert.Equal(3, CostumeChest76543RewardTable.RentalDays);
    }

    private sealed class FixedRandom(int value) : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return value;
        }
    }

    private sealed class CapturingRandom : Random
    {
        public int LastMinValue { get; private set; }
        public int LastMaxValue { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            LastMinValue = minValue;
            LastMaxValue = maxValue;
            return minValue;
        }
    }

    private sealed class NoDrawRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            throw new InvalidOperationException("No random draw should occur for an unrecognized tribe.");
        }
    }
}
