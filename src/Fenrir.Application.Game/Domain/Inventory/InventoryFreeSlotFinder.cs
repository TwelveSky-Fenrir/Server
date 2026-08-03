using System.Collections.Immutable;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class InventoryFreeSlotFinder
{
    public const int GridWidth = 8;

    private const int GridCellCount = GridWidth * GridWidth;

    private const byte LastStorageSlot = 63;

    public static Placement? Find(InventoryState inventory, WorldDataCache worldData, int itemId, int inventoryDate,
        int today)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var definition))
            return null;

        var occupyRange = IsSingleCellSort(definition.Item.Sort) ? 1 : 2;
        var scanRange = GridWidth - occupyRange + 1;
        var pageCount = RentedInventoryPageGate.AccessiblePageCount(inventoryDate, today);

        Span<bool> occupancy = stackalloc bool[GridCellCount];

        for (byte page = 0; page < pageCount; page++)
        {
            var container = inventory.GetContainer(page);
            BuildOccupancy(container, worldData, occupancy);

            for (var y = 0; y < scanRange; y++)
            for (var x = 0; x < scanRange; x++)
            {
                if (!IsBlockFree(occupancy, x, y, occupyRange))
                    continue;

                if (!TryTakeStorageSlot(container, out var slot))
                    return null;

                return new Placement(page, slot, (byte)x, (byte)y);
            }
        }

        return null;
    }

    public static bool IsSingleCellSort(byte itemSort)
    {
        return itemSort is 2 or 7 or 11;
    }

    private static void BuildOccupancy(ImmutableDictionary<byte, ItemStack> container, WorldDataCache worldData,
        Span<bool> occupancy)
    {
        occupancy.Clear();

        foreach (var (_, stack) in container)
        {
            if (stack.XPos >= GridWidth || stack.YPos >= GridWidth)
                continue;

            if (!worldData.ItemsById.TryGetValue(stack.ItemId, out var definition))
                continue;

            int x = stack.XPos;
            int y = stack.YPos;
            occupancy[y * GridWidth + x] = true;

            if (IsSingleCellSort(definition.Item.Sort))
                continue;

            if (x < GridWidth - 1)
                occupancy[y * GridWidth + x + 1] = true;

            if (y >= GridWidth - 1)
                continue;

            occupancy[(y + 1) * GridWidth + x] = true;

            if (x < GridWidth - 1)
                occupancy[(y + 1) * GridWidth + x + 1] = true;
        }
    }

    private static bool IsBlockFree(ReadOnlySpan<bool> occupancy, int x, int y, int occupyRange)
    {
        for (var dy = 0; dy < occupyRange; dy++)
        for (var dx = 0; dx < occupyRange; dx++)
            if (occupancy[(y + dy) * GridWidth + x + dx])
                return false;

        return true;
    }

    private static bool TryTakeStorageSlot(ImmutableDictionary<byte, ItemStack> container, out byte slot)
    {
        for (byte candidate = 0; candidate <= LastStorageSlot; candidate++)
            if (!container.ContainsKey(candidate))
            {
                slot = candidate;
                return true;
            }

        slot = 0;
        return false;
    }

    public readonly record struct Placement(byte Container, byte Slot, byte X, byte Y)
    {
        public int GridIndex => Y * GridWidth + X;
    }
}
