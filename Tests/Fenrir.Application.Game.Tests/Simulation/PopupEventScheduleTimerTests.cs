using Fenrir.Application.Game.Domain.Simulation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Simulation;

public class PopupEventScheduleTimerTests
{
    private static DateTime Utc(int hour, int minute, int second = 0)
    {
        return new DateTime(2026, 7, 10, hour, minute, second, DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    [InlineData(18)]
    public void YanggokWindow_OpensAtMinuteFiftyNine_OfEachOpenHour(int openHour)
    {
        var state = new PopupEventState();
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(openHour, 59));

        Assert.True(state.IsEnabled(PopupEventType.YanggokPvp));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(20)]
    public void YanggokWindow_ClosesAtTopOfEachCloseHour(int closeHour)
    {
        var state = new PopupEventState();
        state.SetEnabled(PopupEventType.YanggokPvp, true);
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(closeHour, 0));

        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));
    }

    [Fact]
    public void YanggokWindow_CountdownMinutes_DoNotFlipTheFlag()
    {
        var state = new PopupEventState();
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(0, 49));
        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));

        timer.Tick(Utc(0, 54));
        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));

        timer.Tick(Utc(0, 58));
        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public void MonsterWindow_OpensAtMinuteFiftyNine_OfEachOpenHour(int openHour)
    {
        var state = new PopupEventState();
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(openHour, 59));

        Assert.True(state.IsEnabled(PopupEventType.MonsterPve));
        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    public void MonsterWindow_ClosesAtTopOfEachCloseHour(int closeHour)
    {
        var state = new PopupEventState();
        state.SetEnabled(PopupEventType.MonsterPve, true);
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(closeHour, 0));

        Assert.False(state.IsEnabled(PopupEventType.MonsterPve));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(21)]
    public void InvasionWindow_OpensAtTopOfEachOpenHour(int openHour)
    {
        var state = new PopupEventState();
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(openHour, 0));

        Assert.True(state.IsEnabled(PopupEventType.InvasionPvp));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(23)]
    public void InvasionWindow_ClosesAtTopOfEachCloseHour(int closeHour)
    {
        var state = new PopupEventState();
        state.SetEnabled(PopupEventType.InvasionPvp, true);
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(closeHour, 0));

        Assert.False(state.IsEnabled(PopupEventType.InvasionPvp));
    }

    [Fact]
    public void RepeatedTicksWithinTheSameMinute_AreNoOps()
    {
        var state = new PopupEventState();
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(0, 59, 0));
        Assert.True(state.IsEnabled(PopupEventType.YanggokPvp));

        state.SetEnabled(PopupEventType.YanggokPvp, false);
        timer.Tick(Utc(0, 59, 30));

        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));
    }

    [Fact]
    public void OutOfWindowHours_NeverToggleAnyFlag()
    {
        var state = new PopupEventState();
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        timer.Tick(Utc(5, 30));

        Assert.False(state.IsEnabled(PopupEventType.YanggokPvp));
        Assert.False(state.IsEnabled(PopupEventType.MonsterPve));
        Assert.False(state.IsEnabled(PopupEventType.InvasionPvp));
    }

    [Fact]
    public void RegularWarAndRuinsPvp_AreNeverToggled_OutOfScopeForThisTimer()
    {
        var state = new PopupEventState();
        var timer = new PopupEventScheduleTimer(state, NullLogger<PopupEventScheduleTimer>.Instance);

        foreach (var hour in new[] { 0, 1, 2, 3, 11, 12, 13, 14, 16, 18, 20, 21, 23 })
        foreach (var minute in new[] { 0, 49, 54, 58, 59 })
            timer.Tick(Utc(hour, minute));

        Assert.False(state.IsEnabled(PopupEventType.RegularWar));
        Assert.False(state.IsEnabled(PopupEventType.RuinsPvp));
    }
}
