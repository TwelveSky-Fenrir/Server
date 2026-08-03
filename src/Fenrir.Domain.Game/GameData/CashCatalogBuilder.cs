using System.Collections.Immutable;

namespace Fenrir.Domain.Game.GameData;

public static class CashCatalogBuilder
{
    public const int MaxCashType = 4;
    public const int MaxCashPage = 20;
    public const int MaxCashItemPerPage = 10;
    public const int MaxCashItemDetail = 4;

    public const int MaxCashNum = MaxCashType * MaxCashItemPerPage * MaxCashPage;

    public static CashCatalog Build(IEnumerable<ItemMallProductRowDto> products)
    {
        var costInfo = new CostInfoEntry[MaxCashNum];
        var grid = new int[MaxCashType * MaxCashPage * MaxCashItemPerPage * MaxCashItemDetail];
        Array.Fill(grid, -1);

        var rowCount = new int[MaxCashType];
        var pageCount = new int[MaxCashType];
        var itemCount = new int[MaxCashType];

        var ordered = products
            .Where(static p => p.ProductType is >= 1 and <= MaxCashType && p.ItemId is not null)
            .OrderBy(static p => p.ItemMallProductId);

        foreach (var product in ordered)
        {
            var typeIndex = product.ProductType - 1;
            var costInfoIndex = typeIndex * MaxCashPage * MaxCashItemPerPage + rowCount[typeIndex];
            rowCount[typeIndex]++;

            if (costInfoIndex >= MaxCashNum)
                continue;

            costInfo[costInfoIndex] = new CostInfoEntry(product.Cost, product.ItemId!.Value, product.Quantity,
                product.ProductType, product.ItemMallProductId);

            if (product.ItemId!.Value is < 1 or > 99999)
                continue;

            if (product.IsActive && pageCount[typeIndex] < MaxCashPage)
            {
                var baseIndex =
                    ((typeIndex * MaxCashPage + pageCount[typeIndex]) * MaxCashItemPerPage + itemCount[typeIndex]) *
                    MaxCashItemDetail;
                grid[baseIndex + 0] = costInfoIndex;
                grid[baseIndex + 1] = product.ItemId!.Value;
                grid[baseIndex + 2] = product.Quantity;
                grid[baseIndex + 3] = product.Cost;
            }

            itemCount[typeIndex]++;
            if (itemCount[typeIndex] >= MaxCashItemPerPage)
            {
                pageCount[typeIndex]++;
                itemCount[typeIndex] = 0;
            }
        }

        return new CashCatalog { CostInfoByIndex = [.. costInfo], DisplayGrid = [.. grid] };
    }

    public static int ResolveVersion(IEnumerable<ItemMallProductRowDto> products)
    {
        foreach (var product in products)
            if (product.ItemMallProductId == 100000 && product.ProductType == 5)
                return product.ItemId ?? 0;

        return 0;
    }

    public static int ResolveCrc(IEnumerable<ItemMallProductRowDto> products)
    {
        foreach (var product in products)
            if (product.ItemMallProductId == 100001 && product.ProductType == 5)
                return product.ItemId ?? 0;

        return 0;
    }

    public static bool ResolveSellEnabled(IEnumerable<ItemMallProductRowDto> products)
    {
        foreach (var product in products)
            if (product.ItemMallProductId == 100002 && product.ProductType == 5)
                return product.IsActive;

        return true;
    }

    public readonly record struct CostInfoEntry(int Cost, int ItemId, int Quantity, int Type, int ItemMallProductId)
    {
        public bool IsAssigned => ItemId >= 1;
    }

    public sealed class CashCatalog
    {
        public required ImmutableArray<CostInfoEntry> CostInfoByIndex { get; init; }

        public required ImmutableArray<int> DisplayGrid { get; init; }
    }
}
