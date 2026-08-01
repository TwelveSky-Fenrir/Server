namespace Fenrir.Application.Game.Domain.Inventory;

public static class RentedInventoryPageGate
{
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

    public static int AccessiblePageCount(int inventoryDate, int today)
    {
        return IsRentedPageActive(inventoryDate, today) ? 2 : 1;
    }
}
