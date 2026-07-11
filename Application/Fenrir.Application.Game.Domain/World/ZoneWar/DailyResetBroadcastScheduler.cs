namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetBroadcastScheduler
{
    private int _lastMinuteOfDay = -1;

    public bool TryConsumeDueFire(DateTime utcNow)
    {
        var minuteOfDay = utcNow.Hour * 60 + utcNow.Minute;
        if (minuteOfDay == _lastMinuteOfDay)
            return false;

        _lastMinuteOfDay = minuteOfDay;
        return utcNow is { Hour: 0, Minute: 1 };
    }
}
