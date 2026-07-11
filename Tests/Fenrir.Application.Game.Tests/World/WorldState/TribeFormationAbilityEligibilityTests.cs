using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Tests.World.WorldState;

/// <summary>
///     Pure gates (a)-(c) of the tribe Formation-ability declaration's five-gate eligibility chain -- see
///     <see cref="TribeFormationAbilityEligibility" /> for what each gate means and its own citations. Gate
///     (d) (Tribe Symbol Battle active) is a single <see cref="WorldStateService.World" /> flag read, not a
///     pure function of <see cref="TribeRvrState" />, so it is covered instead by
///     <c>TribeActionServiceFormationAbilityTests</c> alongside the full five-gate composition.
/// </summary>
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
        // Tribes 1 and 2 are tied at the lowest total (300); tribe 1 must win since it never gets displaced
        // by a later tribe that merely ties it.
        var tribes = Tribes(500, 300, 300, 500);

        Assert.Equal(1, TribeFormationAbilityEligibility.FindLowestPointTribe(tribes));
    }

    [Fact]
    public void FindLowestPointTribe_TribeZeroTiedWithLaterTribe_TribeZeroWins()
    {
        // Tribe 0 is the initial candidate; tribe 1 merely ties it (300 == 300), so tribe 1 never displaces it.
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
        // 20 of a combined 100 == exactly 20% -- must fail (20% is not "under" 20%).
        Assert.False(TribeFormationAbilityEligibility.IsUnderShareThreshold(20, 100));
    }

    [Fact]
    public void IsUnderShareThreshold_NineteenPercent_Passes()
    {
        // 19 of a combined 100 == exactly 19% -- must pass.
        Assert.True(TribeFormationAbilityEligibility.IsUnderShareThreshold(19, 100));
    }

    [Fact]
    public void IsUnderShareThreshold_IntegerTruncation_RoundsDownNotToNearest()
    {
        // 199 of 1000 == 19.9%, which truncates (not rounds) to 19 -- still under 20, so this must pass; a
        // naive round-to-nearest implementation would incorrectly round this up to 20 and fail it.
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
