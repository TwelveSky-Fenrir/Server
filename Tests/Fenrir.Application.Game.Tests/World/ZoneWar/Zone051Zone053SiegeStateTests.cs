using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone051Zone053SiegeStateTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(-1, false)]
    [InlineData(6, false)]
    public void IsValidZone051Slot_MatchesTheSixSlotArray(int slot, bool expected)
    {
        Assert.Equal(expected, Zone051Zone053SiegeState.IsValidZone051Slot(slot));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(9, true)]
    [InlineData(-1, false)]
    [InlineData(10, false)]
    public void IsValidZone053Slot_MatchesTheTenSlotArray(int slot, bool expected)
    {
        Assert.Equal(expected, Zone051Zone053SiegeState.IsValidZone053Slot(slot));
    }

    [Fact]
    public void SetZone051_ThenGet_RoundTripsAtTheRightSlot_LeavesOthersUntouched()
    {
        var state = new Zone051Zone053SiegeState();

        state.SetZone051(3, 5);

        Assert.Equal(5, state.GetZone051(3));
        Assert.Equal(0, state.GetZone051(2));
        Assert.Equal(0, state.GetZone051(4));
    }

    [Fact]
    public void SetZone051_OutOfRangeSlot_Throws()
    {
        var state = new Zone051Zone053SiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone051(6, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone051(-1, 1));
    }

    [Fact]
    public void GetZone051_OutOfRangeSlot_Throws()
    {
        var state = new Zone051Zone053SiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetZone051(6));
    }

    [Fact]
    public void SetZone053_ThenGet_RoundTripsAtTheRightSlot_LeavesOthersUntouched()
    {
        var state = new Zone051Zone053SiegeState();

        state.SetZone053(7, 4);

        Assert.Equal(4, state.GetZone053(7));
        Assert.Equal(0, state.GetZone053(6));
        Assert.Equal(0, state.GetZone053(8));
    }

    [Fact]
    public void SetZone053_OutOfRangeSlot_Throws()
    {
        var state = new Zone051Zone053SiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone053(10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetZone053(-1, 1));
    }

    [Fact]
    public void GetZone053_OutOfRangeSlot_Throws()
    {
        var state = new Zone051Zone053SiegeState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetZone053(10));
    }

    [Fact]
    public void Zone051AndZone053_AreIndependentStorage_SameSlotIndexDoesNotCollide()
    {
        var state = new Zone051Zone053SiegeState();

        state.SetZone051(2, 9);
        state.SetZone053(2, 4);

        Assert.Equal(9, state.GetZone051(2));
        Assert.Equal(4, state.GetZone053(2));
    }
}
