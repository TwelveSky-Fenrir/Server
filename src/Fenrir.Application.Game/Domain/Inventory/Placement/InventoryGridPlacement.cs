using System.Collections.Immutable;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class InventoryGridPlacement
{
    public const int GridWidth = 8;

    public const int GridCellCount = GridWidth * GridWidth;

    public const byte LastStorageSlot = 63;

    public static bool IsSingleCellSort(byte itemSort)
    {
        return itemSort is 2 or 7 or 11;
    }

    public static int FootprintFor(byte itemSort)
    {
        return IsSingleCellSort(itemSort) ? 1 : 2;
    }

    public static int ScanRangeFor(int footprint)
    {
        return GridWidth - footprint + 1;
    }

    public static void BuildOccupancy(ImmutableDictionary<byte, ItemStack> container, WorldDataCache worldData,
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

    public static bool IsBlockFree(ReadOnlySpan<bool> occupancy, int x, int y, int footprint)
    {
        if (x < 0 || y < 0 || x + footprint > GridWidth || y + footprint > GridWidth)
            return false;

        for (var dy = 0; dy < footprint; dy++)
        for (var dx = 0; dx < footprint; dx++)
            if (occupancy[(y + dy) * GridWidth + x + dx])
                return false;

        return true;
    }

    public static bool TryFindFreeCell(ReadOnlySpan<bool> occupancy, int footprint, out byte x, out byte y)
    {
        var scanRange = ScanRangeFor(footprint);

        for (var scanY = 0; scanY < scanRange; scanY++)
        for (var scanX = 0; scanX < scanRange; scanX++)
            if (IsBlockFree(occupancy, scanX, scanY, footprint))
            {
                x = (byte)scanX;
                y = (byte)scanY;
                return true;
            }

        x = 0;
        y = 0;
        return false;
    }

    public static bool TryTakeStorageSlot(ImmutableDictionary<byte, ItemStack> container, out byte slot)
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
}
