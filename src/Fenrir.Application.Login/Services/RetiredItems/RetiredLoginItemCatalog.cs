namespace Fenrir.Application.Login.Services.RetiredItems;

public static class RetiredLoginItemCatalog
{
    public static bool IsRetired(int itemId)
    {
        return itemId is 1451 or 2268;
    }
}
