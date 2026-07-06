using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class MinuteCountdownTests
{
    [Fact]
    public void Advance_LessThanOneMinute_ReturnsZero_AndDoesNotAdvanceMinutesElapsed()
    {
        var countdown = new MinuteCountdown();

        var result = countdown.Advance(TimeSpan.FromSeconds(59));

        Assert.Equal(0, result);
        Assert.Equal(0, countdown.MinutesElapsed);
    }

    [Fact]
    public void Advance_ExactlyOneMinute_ReturnsOne_AndIncrementsMinutesElapsed()
    {
        var countdown = new MinuteCountdown();

        var result = countdown.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, result);
        Assert.Equal(1, countdown.MinutesElapsed);
    }

    [Fact]
    public void Advance_AccumulatesAcrossCalls_UntilAMinuteElapses()
    {
        var countdown = new MinuteCountdown();

        Assert.Equal(0, countdown.Advance(TimeSpan.FromSeconds(30)));
        Assert.Equal(0, countdown.Advance(TimeSpan.FromSeconds(29)));
        Assert.Equal(1, countdown.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(1, countdown.MinutesElapsed);
    }

    [Fact]
    public void Advance_StalledHost_CatchesUpMultipleMinutesInOneCall()
    {
        var countdown = new MinuteCountdown();

        var result = countdown.Advance(TimeSpan.FromMinutes(3.5));

        Assert.Equal(3, result);
        Assert.Equal(3, countdown.MinutesElapsed);
    }

    [Fact]
    public void Reset_ClearsAccumulatedTimeAndMinutesElapsed()
    {
        var countdown = new MinuteCountdown();
        countdown.Advance(TimeSpan.FromMinutes(2.5));

        countdown.Reset();

        Assert.Equal(0, countdown.MinutesElapsed);
        Assert.Equal(0, countdown.Advance(TimeSpan.FromSeconds(59)));
    }
}
