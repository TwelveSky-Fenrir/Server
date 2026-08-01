namespace Fenrir.Application.Login.Services.RetiredItems;

public static class RetiredLoginItemCatalog
{
    // MyGame::CheckDeleteItem, the only two live cases (Server/ts25login/S07_MyGame01.cpp:119-121).
    // Twin of AvatarInfoFactory.IsLoginRosterBlacklistedItemId, which filters the roster projection.
    public static bool IsRetired(int itemId)
    {
        return itemId is 1451 or 2268;
    }
}
