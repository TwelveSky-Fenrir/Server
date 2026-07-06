namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>Legacy's raw YYYYMMDD int date encoding (datetime.h::ReturnNowDate), always UTC here.</summary>
public static class GameDate
{
    /// <summary>The legacy's negative-one failure sentinel for a date projection that could not be represented.</summary>
    public const int Invalid = -1;

    public static int Today()
    {
        var now = DateTime.UtcNow;
        return now.Year * 10000 + now.Month * 100 + now.Day;
    }

    /// <summary>
    ///     Server/Header/datetime.h:195-218 -- projects <paramref name="compactDate" /> forward by
    ///     <paramref name="days" /> using real calendar arithmetic (not naive digit addition), returning
    ///     <see langword="false" /> when <paramref name="compactDate" /> cannot be parsed as a real calendar
    ///     date or the projected result overflows the representable range -- the caller should treat that as
    ///     the legacy's <see cref="Invalid" /> sentinel.
    /// </summary>
    public static bool TryAddDays(int compactDate, int days, out int result)
    {
        result = Invalid;

        var year = compactDate / 10000;
        var month = compactDate / 100 % 100;
        var day = compactDate % 100;

        try
        {
            var projected = new DateOnly(year, month, day).AddDays(days);
            result = projected.Year * 10000 + projected.Month * 100 + projected.Day;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
