using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneCenterSiegeStateTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 7, true)]
    [InlineData(-1, 0, false)]
    [InlineData(4, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(0, 8, false)]
    public void IsValidZone175Cell_MatchesTheFourByEightGrid(int instance, int slot, bool expected)
    {
        Assert.Equal(expected, ZoneCenterSiegeState.IsValidZone175Cell(instance, slot));
    }

    [Fact]
    public void SetZone175_ThenGet_RoundTripsAtTheRightCell_LeavesOthersUntouched()
    {
        var state = new ZoneCenterSiegeState();

        state.SetZone175(2, 5, 77);

        Assert.Equal(77, state.GetZone175(2, 5));
        Assert.Equal(0, state.GetZone175(2, 4));
        Assert.Equal(0, state.GetZone175(1, 5));
    }

    [Fact]
    public void ResetZone175_ReturnsTheCellToIdle()
    {
        var state = new ZoneCenterSiegeState();
        state.SetZone175(0, 0, 64);

        state.ResetZone175(0, 0);

        Assert.Equal(0, state.GetZone175(0, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 8)]
    public void SetZone175_OutOfRangeCell_Throws(int instance, int slot)
    {
        var state = new ZoneCenterSiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone175(instance, slot, 1));
    }

    [Fact]
    public void SetZone267_ThenGet_RoundTripsPerTribe()
    {
        var state = new ZoneCenterSiegeState();

        state.SetZone267(2, 402);

        Assert.Equal(402, state.GetZone267(2));
        Assert.Equal(0, state.GetZone267(0));
    }

    [Fact]
    public void ResetZone267_ReturnsToIdle()
    {
        var state = new ZoneCenterSiegeState();
        state.SetZone267(1, 405);

        state.ResetZone267(1);

        Assert.Equal(0, state.GetZone267(1));
    }

    [Fact]
    public void SetZone267_OutOfRangeTribe_Throws()
    {
        var state = new ZoneCenterSiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone267(4, 402));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(19, true)]
    [InlineData(-1, false)]
    [InlineData(20, false)]
    public void IsValidZone241Instance_MatchesTheTwentyInstanceGrid(int instance, bool expected)
    {
        Assert.Equal(expected, ZoneCenterSiegeState.IsValidZone241Instance(instance));
    }

    [Fact]
    public void SetZone241_ThenGet_RoundTrips()
    {
        var state = new ZoneCenterSiegeState();

        state.SetZone241(5, DenOfRebirthChallengeState.ChallengeStarted);

        Assert.Equal(DenOfRebirthChallengeState.ChallengeStarted, state.GetZone241(5));
        Assert.Equal(DenOfRebirthChallengeState.Idle, state.GetZone241(0));
    }

    [Fact]
    public void ResetZone241_ReturnsToIdle()
    {
        var state = new ZoneCenterSiegeState();
        state.SetZone241(3, DenOfRebirthChallengeState.Ended);

        state.ResetZone241(3);

        Assert.Equal(DenOfRebirthChallengeState.Idle, state.GetZone241(3));
    }

    [Fact]
    public void SetZone241_OutOfRangeInstance_Throws()
    {
        var state = new ZoneCenterSiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone241(20, DenOfRebirthChallengeState.Idle));
    }

    [Fact]
    public void Zone335_DefaultsToIdle_SetThenResetRoundTrips()
    {
        var state = new ZoneCenterSiegeState();
        Assert.Equal(0, state.Zone335);

        state.SetZone335(1503);
        Assert.Equal(1503, state.Zone335);

        state.ResetZone335();
        Assert.Equal(0, state.Zone335);
    }

    [Fact]
    public void SetZone038DtmValue_ThenGet_RoundTripsPerTribe()
    {
        var state = new ZoneCenterSiegeState();

        state.SetZone038DtmValue(3, 42);

        Assert.Equal(42, state.GetZone038DtmValue(3));
        Assert.Equal(0, state.GetZone038DtmValue(0));
    }

    [Fact]
    public void TribeBonusRatioSetters_RoundTripIndependentlyPerField()
    {
        var state = new ZoneCenterSiegeState();

        state.SetExperienceBonusRatio(0, 1.5f);
        state.SetItemDropBonusRatio(0, 2.5f);
        state.SetMyoungItemDropBonusRatio(0, 3.5f);

        Assert.Equal(1.5f, state.GetExperienceBonusRatio(0));
        Assert.Equal(2.5f, state.GetItemDropBonusRatio(0));
        Assert.Equal(3.5f, state.GetMyoungItemDropBonusRatio(0));
    }

    [Fact]
    public void SetKillOtherTribeBonus_WritesTheTargetTribe_AndZeroesTheOtherThree_InTheSameWrite()
    {
        var state = new ZoneCenterSiegeState();
        state.SetKillOtherTribeBonus(1, 10);
        state.SetKillOtherTribeBonus(3, 20);

        state.SetKillOtherTribeBonus(2, 99);

        Assert.Equal(0, state.GetKillOtherTribeBonus(0));
        Assert.Equal(0, state.GetKillOtherTribeBonus(1));
        Assert.Equal(99, state.GetKillOtherTribeBonus(2));
        Assert.Equal(0, state.GetKillOtherTribeBonus(3));
    }

    [Fact]
    public void GetZone038DtmValue_OutOfRangeTribe_Throws()
    {
        var state = new ZoneCenterSiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetZone038DtmValue(4));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(12, true)]
    [InlineData(-1, false)]
    [InlineData(13, false)]
    public void IsValidZone049Slot_MatchesTheThirteenSlotArray(int slot, bool expected)
    {
        Assert.Equal(expected, ZoneCenterSiegeState.IsValidZone049Slot(slot));
    }

    [Fact]
    public void SetZone049State_WithoutStampTime_WritesStateOnly_LeavesTimeAndOtherSlotsUntouched()
    {
        var state = new ZoneCenterSiegeState();

        state.SetZone049State(5, 3, false);

        Assert.Equal(3, state.GetZone049State(5));
        Assert.Equal(0, state.GetZone049StateTime(5));
        Assert.Equal(0, state.GetZone049State(4));
    }

    [Fact]
    public void SetZone049State_WithStampTime_WritesStateAndANonZeroTimestamp()
    {
        var state = new ZoneCenterSiegeState();

        state.SetZone049State(2, 5, true);

        Assert.Equal(5, state.GetZone049State(2));
        Assert.NotEqual(0, state.GetZone049StateTime(2));
    }

    [Fact]
    public void SetZone049State_OutOfRangeSlot_Throws()
    {
        var state = new ZoneCenterSiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone049State(13, 1, false));
    }

    [Fact]
    public void GetZone049State_OutOfRangeSlot_Throws()
    {
        var state = new ZoneCenterSiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetZone049State(-1));
    }
}
