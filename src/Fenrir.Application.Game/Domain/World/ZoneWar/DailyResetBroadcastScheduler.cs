namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class DailyResetBroadcastScheduler
{
    private static readonly TimeSpan FireAtLocalTimeOfDay = TimeSpan.FromMinutes(1);

    private DateOnly _completedLocalDate = DateOnly.MinValue;

    public bool IsDue(DateTimeOffset localNow)
    {
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        return localNow.TimeOfDay >= FireAtLocalTimeOfDay && localDate > _completedLocalDate;
    }

    public void MarkCompleted(DateOnly localDate)
    {
        if (localDate > _completedLocalDate)
            _completedLocalDate = localDate;
    }
}
