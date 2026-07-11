using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Tests.AntiCheat;

public class PlayerCadenceTimersTests
{
    [Theory]
    [InlineData(AntiCheatCadence.OneSecond, 1)]
    [InlineData(AntiCheatCadence.OneSecondProtect, 1)]
    [InlineData(AntiCheatCadence.ThreeSecond, 3)]
    [InlineData(AntiCheatCadence.TenSecond, 10)]
    [InlineData(AntiCheatCadence.ThirtySecond, 30)]
    [InlineData(AntiCheatCadence.OneMinute, 60)]
    [InlineData(AntiCheatCadence.OneMinuteHealth, 60)]
    [InlineData(AntiCheatCadence.OneMinuteSecondary, 60)]
    [InlineData(AntiCheatCadence.OneMinuteTertiary, 60)]
    [InlineData(AntiCheatCadence.FiveMinuteHack, 300)]
    [InlineData(AntiCheatCadence.TenMinute, 600)]
    [InlineData(AntiCheatCadence.SixtyMinute, 3600)]
    public void IntervalOf_MatchesNameEncodedDuration(AntiCheatCadence cadence, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), PlayerCadenceTimers.IntervalOf(cadence));
    }

    [Fact]
    public void ResetAll_BaselinesEveryCadenceToNow()
    {
        var now = TimeSpan.FromMinutes(42);
        var timers = new PlayerCadenceTimers();
        timers.ResetAll(now);

        foreach (var cadence in Enum.GetValues<AntiCheatCadence>())
        {
            Assert.Equal(now, timers.Baseline(cadence));
            Assert.False(timers.HasElapsed(cadence, now), $"{cadence} must not be elapsed at its own baseline");
        }
    }

    [Fact]
    public void HasElapsed_TrueOnceIntervalPasses()
    {
        var now = TimeSpan.FromMinutes(10);
        var timers = new PlayerCadenceTimers();
        timers.ResetAll(now);

        Assert.False(timers.HasElapsed(AntiCheatCadence.ThreeSecond, now + TimeSpan.FromSeconds(2)));
        Assert.True(timers.HasElapsed(AntiCheatCadence.ThreeSecond, now + TimeSpan.FromSeconds(3)));
        Assert.True(timers.HasElapsed(AntiCheatCadence.ThreeSecond, now + TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Restamp_RebaselinesOnlyTheGivenCadence()
    {
        var now = TimeSpan.FromMinutes(10);
        var timers = new PlayerCadenceTimers();
        timers.ResetAll(now);

        var later = now + TimeSpan.FromSeconds(30);
        timers.Restamp(AntiCheatCadence.OneSecond, later);

        Assert.Equal(later, timers.Baseline(AntiCheatCadence.OneSecond));
        Assert.Equal(now, timers.Baseline(AntiCheatCadence.ThreeSecond));
    }
}
