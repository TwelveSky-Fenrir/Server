namespace Fenrir.Application.Game.Simulation;

/// <summary>Legacy's raw YYYYMMDD int date encoding (datetime.h::ReturnNowDate), always UTC here.</summary>
public static class GameDate
{
    public static int Today()
    {
        var now = DateTime.UtcNow;
        return now.Year * 10000 + now.Month * 100 + now.Day;
    }
}
