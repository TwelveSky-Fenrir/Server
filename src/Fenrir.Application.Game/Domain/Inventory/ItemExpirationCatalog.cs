namespace Fenrir.Application.Game.Domain.Inventory;

public static class ItemExpirationCatalog
{
    private const int TimedItemIdStart = 76500;

    private const int TimedItemIdEndInclusive = 76540;

    public static bool IsTimedItem(int itemId)
    {
        return itemId is >= TimedItemIdStart and <= TimedItemIdEndInclusive;
    }

    public static bool IsExpiredAtWorldEntry(int itemId, int expireDate, int today)
    {
        return IsTimedItem(itemId) && expireDate <= today;
    }
}
