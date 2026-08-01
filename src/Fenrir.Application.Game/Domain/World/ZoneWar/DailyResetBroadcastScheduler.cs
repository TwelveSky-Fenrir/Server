namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetBroadcastScheduler
{
    private static readonly TimeSpan FireAtLocalTimeOfDay = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan EagerResetGrace = TimeSpan.FromHours(1);

    private DateOnly _lastFiredLocalDate = DateOnly.MinValue;

    public bool IsDue(DateTimeOffset localNow)
    {
        return localNow.TimeOfDay >= FireAtLocalTimeOfDay
               && DateOnly.FromDateTime(localNow.DateTime) > _lastFiredLocalDate;
    }

    public bool AllowsEagerReset(DateTimeOffset localNow)
    {
        return localNow.TimeOfDay < FireAtLocalTimeOfDay + EagerResetGrace;
    }

    public void MarkFired(DateTimeOffset localNow)
    {
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        if (localDate > _lastFiredLocalDate)
            _lastFiredLocalDate = localDate;
    }
}
