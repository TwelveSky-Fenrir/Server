namespace Fenrir.Application.Game.Domain.Simulation;

public static class GameDate
{
    public const int Invalid = -1;

    public static int Today()
    {
        var now = DateTime.UtcNow;
        return now.Year * 10000 + now.Month * 100 + now.Day;
    }

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
