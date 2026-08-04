namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class Zone195TimeEventGate
{
    public static bool IsOpen => IsOpenAt(TimeProvider.System);

    public static bool IsOpenAt(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var localNow = timeProvider.GetLocalNow();
        return localNow.DayOfWeek == DayOfWeek.Sunday && localNow.Hour is >= 20 and < 22;
    }
}
