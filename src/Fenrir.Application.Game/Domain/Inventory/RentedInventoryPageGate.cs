namespace Fenrir.Application.Game.Domain.Inventory;

public static class RentedInventoryPageGate
{
    // CheckInv (Server/ts25zone/S04_MyWork02.cpp:155-162) : la derniere page (MAX_INVENTORY_PAGE_NUM-1 == 1)
    // est louee ; aInventoryDate est un YYYYMMDD et l'egalite avec le jour courant reste accessible.
    public static bool IsRentedPageActive(int inventoryDate, int today)
    {
        return inventoryDate >= today;
    }

    public static bool IsPageAccessible(int page, int inventoryDate, int today)
    {
        return page switch
        {
            ContainerMatrix.InventoryPage0 => true,
            ContainerMatrix.InventoryPage1 => IsRentedPageActive(inventoryDate, today),
            _ => false
        };
    }

    // MyUtil::FindEmptyInvenForItem (Server/ts25zone/S07_MyGame03.cpp:4226-4227) : iMaxPage ne passe a 2
    // que si la location court encore, sinon tout octroi doit se limiter a la page 0.
    public static int AccessiblePageCount(int inventoryDate, int today)
    {
        return IsRentedPageActive(inventoryDate, today) ? 2 : 1;
    }
}
