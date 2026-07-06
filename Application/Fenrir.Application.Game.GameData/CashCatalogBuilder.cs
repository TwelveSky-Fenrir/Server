using System.Collections.Immutable;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     Builds the cash shop's two views: an 800-slot cost-info table (a purchase resolves price/item from this, never
///     client-submitted values) and the 4x20x10x4 display grid.
/// </summary>
public static class CashCatalogBuilder
{
    public const int MaxCashType = 4;
    public const int MaxCashPage = 20;
    public const int MaxCashItemPerPage = 10;
    public const int MaxCashItemDetail = 4;

    public const int MaxCashNum = MaxCashType * MaxCashItemPerPage * MaxCashPage;

    /// <summary>Only ProductType 1..4 participate; 5 is the version-sentinel row (see <see cref="ResolveVersion" />).</summary>
    public static CashCatalog Build(IEnumerable<ItemMallProductRowDto> products)
    {
        var costInfo = new CostInfoEntry[MaxCashNum];
        var grid = new int[MaxCashType * MaxCashPage * MaxCashItemPerPage * MaxCashItemDetail];
        Array.Fill(grid, -1);

        // rowCount advances for every row regardless of ItemID validity; pageCount/itemCount only advance
        // past the range check, so an out-of-range row consumes a costInfoIndex slot but leaves no display gap.
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
                continue; // overflow guard -- never hit by real seed data (159 rows across 4 types, cap 200/type)

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

        return new CashCatalog { CostInfoByIndex = [.. costInfo], DisplayGrid = grid };
    }

    /// <summary>
    ///     Version is the ItemId of the ProductType=5/ItemMallProductId=100000 sentinel row (repurposed column, not a
    ///     real item); 0 if absent.
    /// </summary>
    public static int ResolveVersion(IEnumerable<ItemMallProductRowDto> products)
    {
        foreach (var product in products)
            if (product.ItemMallProductId == 100000 && product.ProductType == 5)
                return product.ItemId ?? 0;

        return 0;
    }

    /// <summary>
    ///     Fenrir-chosen CRC sentinel slot (ItemMallProductId=100001/ProductType=5), mirroring
    ///     <see cref="ResolveVersion" />'s existing 100000/5 convention. The behavior contract this ports
    ///     (cash/blood catalog hot-reload) only describes "a second, separate reserved row, same type" for the
    ///     client CRC without giving its literal row number, so this specific slot number is a Fenrir-side
    ///     design choice, not a verified legacy literal -- and no seed row reserves it yet in
    ///     world.ItemMallProducts, so this always resolves to 0 today until a seed row is added. That is
    ///     harmless: see <see cref="Fenrir.Application.Game.Domain.Commerce.CommerceCatalogCache" />'s own
    ///     remarks for why nothing downstream currently consumes this value.
    /// </summary>
    public static int ResolveCrc(IEnumerable<ItemMallProductRowDto> products)
    {
        foreach (var product in products)
            if (product.ItemMallProductId == 100001 && product.ProductType == 5)
                return product.ItemId ?? 0;

        return 0;
    }

    /// <summary>
    ///     <see cref="ItemMallProductId" /> is a Fenrir-only addition for game.CashLog's audit trail -- never sent on the
    ///     wire.
    /// </summary>
    public readonly record struct CostInfoEntry(int Cost, int ItemId, int Quantity, int Type, int ItemMallProductId)
    {
        public bool IsAssigned => ItemId >= 1;
    }

    public sealed class CashCatalog
    {
        /// <summary>Flat 800-entry master table, index == the wire's CostInfoIndex.</summary>
        public required ImmutableArray<CostInfoEntry> CostInfoByIndex { get; init; }

        /// <summary>
        ///     -1 marks an unfilled/inactive slot. Built once at boot, never mutated -- safe to share without a defensive
        ///     copy.
        /// </summary>
        public required int[] DisplayGrid { get; init; }
    }
}
