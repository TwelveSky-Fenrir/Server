using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class TribeFormationAbilityEligibilityTests
{
    private static TribeRvrState[] Tribes(params int[] points)
    {
        var tribes = new TribeRvrState[points.Length];
        for (byte i = 0; i < points.Length; i++)
            tribes[i] = new TribeRvrState(i, null, true, points[i], false);

        return tribes;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AllTribesAboveFloor_OneTribeExactlyAtFloor_FailsRegardlessOfWhichTribe(int floorTribeIndex)
    {
        var points = new[] { 1000, 1000, 1000, 1000 };
        points[floorTribeIndex] = TribeFormationAbilityEligibility.PointFloor;

        Assert.False(TribeFormationAbilityEligibility.AllTribesAboveFloor(Tribes(points)));
    }

    [Fact]
    public void AllTribesAboveFloor_EveryTribeOneOverFloor_Passes()
    {
        var floorPlusOne = TribeFormationAbilityEligibility.PointFloor + 1;

        Assert.True(TribeFormationAbilityEligibility.AllTribesAboveFloor(
            Tribes(floorPlusOne, floorPlusOne, floorPlusOne, floorPlusOne)));
    }

    [Fact]
    public void FindLowestPointTribe_TwoTribesTied_LowerIdWins()
    {
        var tribes = Tribes(500, 300, 300, 500);

        Assert.Equal(1, TribeFormationAbilityEligibility.FindLowestPointTribe(tribes));
    }

    [Fact]
    public void FindLowestPointTribe_TribeZeroTiedWithLaterTribe_TribeZeroWins()
    {
        var tribes = Tribes(300, 300, 500, 500);

        Assert.Equal(0, TribeFormationAbilityEligibility.FindLowestPointTribe(tribes));
    }

    [Fact]
    public void FindLowestPointTribe_StrictlyLowerLaterTribe_Displaces()
    {
        var tribes = Tribes(500, 500, 500, 100);

        Assert.Equal(3, TribeFormationAbilityEligibility.FindLowestPointTribe(tribes));
    }

    [Fact]
    public void IsUnderShareThreshold_ExactlyTwentyPercent_Fails()
    {
        Assert.False(TribeFormationAbilityEligibility.IsUnderShareThreshold(20, 100));
    }

    [Fact]
    public void IsUnderShareThreshold_NineteenPercent_Passes()
    {
        Assert.True(TribeFormationAbilityEligibility.IsUnderShareThreshold(19, 100));
    }

    [Fact]
    public void IsUnderShareThreshold_IntegerTruncation_RoundsDownNotToNearest()
    {
        Assert.True(TribeFormationAbilityEligibility.IsUnderShareThreshold(199, 1000));
    }

    [Fact]
    public void IsUnderShareThreshold_JustOverTwentyPercent_Fails()
    {
        Assert.False(TribeFormationAbilityEligibility.IsUnderShareThreshold(201, 1000));
    }

    [Fact]
    public void CombinedPoints_SumsAllFourTribes()
    {
        Assert.Equal(2200, TribeFormationAbilityEligibility.CombinedPoints(Tribes(500, 300, 300, 1100)));
    }
}
