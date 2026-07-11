using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class DailyResetBroadcastSchedulerTests
{
    [Fact]
    public void AtHourZeroMinuteOne_FiresOnce()
    {
        var scheduler = new DailyResetBroadcastScheduler();

        Assert.True(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 0, 1, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void RepeatedPollsWithinTheSameMinute_DoNotRefire()
    {
        var scheduler = new DailyResetBroadcastScheduler();

        Assert.True(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 0, 1, 0, DateTimeKind.Utc)));
        Assert.False(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 0, 1, 15, DateTimeKind.Utc)));
        Assert.False(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 0, 1, 45, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(23, 1)]
    public void AnyOtherHourOrMinute_NeverFires(int hour, int minute)
    {
        var scheduler = new DailyResetBroadcastScheduler();

        Assert.False(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, hour, minute, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ReArmsAutomatically_ForTheNextDaysOccurrence()
    {
        var scheduler = new DailyResetBroadcastScheduler();

        Assert.True(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 0, 1, 0, DateTimeKind.Utc)));

        // A whole day of intervening polls, including the same minute-of-day recurring at other hours.
        Assert.False(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 0, 2, 0, DateTimeKind.Utc)));
        Assert.False(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc)));
        Assert.False(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 10, 23, 59, 0, DateTimeKind.Utc)));

        Assert.True(scheduler.TryConsumeDueFire(new DateTime(2026, 7, 11, 0, 1, 0, DateTimeKind.Utc)));
    }
}
