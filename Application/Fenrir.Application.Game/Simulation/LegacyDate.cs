namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     The legacy's own "raw YYYYMMDD int" date encoding (<c>datetime.h::ReturnNowDate</c>, used verbatim
///     across <c>ShopDate</c>/<c>InventoryDate</c>/<c>StoreDate</c>/daily-reward-claim gates) -- an
///     absolute calendar date, never a duration, always UTC-based here (Fenrir has no per-shard local
///     timezone concept).
/// </summary>
public static class LegacyDate
{
    public static int Today()
    {
        var now = DateTime.UtcNow;
        return now.Year * 10000 + now.Month * 100 + now.Day;
    }
}
