using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory.Placement;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class InventoryFreeSlotFinder
{
    public const int GridWidth = InventoryGridPlacement.GridWidth;

    public static Placement? Find(InventoryState inventory, WorldDataCache worldData, int itemId, int inventoryDate,
        int today)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var definition))
            return null;

        var footprint = InventoryGridPlacement.FootprintFor(definition.Item.Sort);
        var pageCount = RentedInventoryPageGate.AccessiblePageCount(inventoryDate, today);

        Span<bool> occupancy = stackalloc bool[InventoryGridPlacement.GridCellCount];

        for (byte page = 0; page < pageCount; page++)
        {
            var container = inventory.GetContainer(page);
            InventoryGridPlacement.BuildOccupancy(container, worldData, occupancy);

            if (!InventoryGridPlacement.TryFindFreeCell(occupancy, footprint, out var x, out var y))
                continue;

            if (!InventoryGridPlacement.TryTakeStorageSlot(container, out var slot))
                return null;

            return new Placement(page, slot, x, y);
        }

        return null;
    }

    public static Placement? FindInPages(ImmutableDictionary<byte, ItemStack> page0,
        ImmutableDictionary<byte, ItemStack> page1, WorldDataCache worldData, int itemId, bool secondPageAccessible)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var definition))
            return null;

        var footprint = InventoryGridPlacement.FootprintFor(definition.Item.Sort);
        var pageCount = secondPageAccessible ? 2 : 1;

        Span<bool> occupancy = stackalloc bool[InventoryGridPlacement.GridCellCount];

        for (byte page = 0; page < pageCount; page++)
        {
            var container = page == ContainerMatrix.InventoryPage0 ? page0 : page1;
            InventoryGridPlacement.BuildOccupancy(container, worldData, occupancy);

            if (!InventoryGridPlacement.TryFindFreeCell(occupancy, footprint, out var x, out var y))
                continue;

            if (!InventoryGridPlacement.TryTakeStorageSlot(container, out var slot))
                return null;

            return new Placement(page, slot, x, y);
        }

        return null;
    }

    public static bool CanPlaceAt(InventoryState inventory, WorldDataCache worldData, int itemId, byte page,
        byte x, byte y)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var definition))
            return false;

        var footprint = InventoryGridPlacement.FootprintFor(definition.Item.Sort);

        Span<bool> occupancy = stackalloc bool[InventoryGridPlacement.GridCellCount];
        InventoryGridPlacement.BuildOccupancy(inventory.GetContainer(page), worldData, occupancy);

        return InventoryGridPlacement.IsBlockFree(occupancy, x, y, footprint);
    }

    public static bool IsSingleCellSort(byte itemSort)
    {
        return InventoryGridPlacement.IsSingleCellSort(itemSort);
    }

    public readonly record struct Placement(byte Container, byte Slot, byte X, byte Y)
    {
        public int GridIndex => Y * GridWidth + X;
    }
}
