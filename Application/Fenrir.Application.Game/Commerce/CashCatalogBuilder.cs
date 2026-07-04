using System.Collections.Immutable;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Commerce;

/// <summary>
///     Pure port of <c>MyDB::GetItemMall</c> (Server/ts25extra/S08_MyDB.cpp:137-235, verified byte-for-bit)
///     -- builds the cash-shop's two derived views from world.ItemMallProducts: a flat 800-slot cost-info
///     master table (<see cref="CostInfoByIndex" />, indexed by the wire's <c>CostInfoIndex</c> --
///     ANTI-CHEAT: a purchase resolves price/item from THIS, never client-submitted values) and the
///     4x20x10x4 flattened display grid (<see cref="DisplayGrid" />). Computed once at boot
///     (<see cref="WorldDataCacheBuilder" />) since the catalog is boot-time-static in this pass.
/// </summary>
public static class CashCatalogBuilder
{
    public const int MaxCashType = 4;
    public const int MaxCashPage = 20;
    public const int MaxCashItemPerPage = 10;
    public const int MaxCashItemDetail = 4;

    /// <summary><c>MAX_CASH_NUM = MAX_CASH_TYPE * MAX_CASH_ITEM_PER_PAGE * MAX_CASH_PAGE</c> (DEFINE.h:555).</summary>
    public const int MaxCashNum = MaxCashType * MaxCashItemPerPage * MaxCashPage;

    /// <summary>
    ///     One <c>mCostInfoValue[costInfoIndex]</c> row: [Cost, ItemId, Quantity, Type]; all-zero when
    ///     unassigned. <see cref="ItemMallProductId" /> is a Fenrir-only addition (not in the legacy row) so
    ///     game.CashLog's audit trail can reference the real catalog row -- never sent on the wire.
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
        ///     Flattened [type][page][item][detail] (row-major), length 3200 -- ZC_GET_CASH_ITEM_INFO_RECV.CashItemInfo
        ///     verbatim. -1 marks an unfilled/inactive slot (matches the legacy's FillMemory(...,-1) init). A
        ///     plain <c>int[]</c>, safe to hand out and reuse without a defensive copy: built once at boot
        ///     and never mutated afterwards (same read-mostly trust every <see cref="WorldDataCache" /> field
        ///     carries).
        /// </summary>
        public required int[] DisplayGrid { get; init; }
    }

    /// <summary>
    ///     <paramref name="products" /> need not be pre-sorted -- orders by ItemMallProductId ascending
    ///     within each ProductType (the legacy's "ORDER BY Number ASC, Cost ASC", where Number/ItemMallProductId
    ///     is already unique, so "Cost ASC" never actually applies). Only ProductType 1..4 participate (5 is
    ///     the version-sentinel row, see <see cref="ResolveVersion" />).
    /// </summary>
    public static CashCatalog Build(IEnumerable<ItemMallProductRowDto> products)
    {
        var costInfo = new CostInfoEntry[MaxCashNum];
        var grid = new int[MaxCashType * MaxCashPage * MaxCashItemPerPage * MaxCashItemDetail];
        Array.Fill(grid, -1);

        // rowCount mirrors the legacy's `iRows` (first pass): advances for every fetched row of this type,
        // independent of the ItemID validity check below. pageCount/itemCount is the separate display
        // cursor (second pass): it only advances past the ItemID range check (verified S08_MyDB.cpp:182-185),
        // so an out-of-range row consumes its own costInfoIndex slot but leaves no gap in the display grid.
        var rowCount = new int[MaxCashType];
        var pageCount = new int[MaxCashType];
        var itemCount = new int[MaxCashType];

        var ordered = products
            .Where(static p => p.ProductType is >= 1 and <= MaxCashType && p.ItemId is not null)
            .OrderBy(static p => p.ItemMallProductId);

        foreach (var product in ordered)
        {
            var typeIndex = product.ProductType - 1;
            var costInfoIndex = typeIndex * (MaxCashPage * MaxCashItemPerPage) + rowCount[typeIndex];
            rowCount[typeIndex]++;

            if (costInfoIndex >= MaxCashNum)
                continue; // overflow guard -- never hit by real seed data (159 rows across 4 types, cap 200/type)

            costInfo[costInfoIndex] = new CostInfoEntry(product.Cost, product.ItemId!.Value, product.Quantity,
                product.ProductType, product.ItemMallProductId);

            if (product.ItemId!.Value is < 1 or > 99999)
                continue;

            if (product.IsActive && pageCount[typeIndex] < MaxCashPage)
            {
                var baseIndex = ((typeIndex * MaxCashPage + pageCount[typeIndex]) * MaxCashItemPerPage + itemCount[typeIndex]) * MaxCashItemDetail;
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
    ///     <c>MyDB::GetItemMallVersion</c> (S08_MyDB.cpp:237-250, verified): the version is the ItemId of the
    ///     ProductType=5/ItemMallProductId=100000 sentinel row (a repurposed column, not a real item
    ///     reference -- see world.ItemMallProducts' own migration header). 0 if that row is absent.
    /// </summary>
    public static int ResolveVersion(IEnumerable<ItemMallProductRowDto> products)
    {
        foreach (var product in products)
            if (product.ItemMallProductId == 100000 && product.ProductType == 5)
                return product.ItemId ?? 0;

        return 0;
    }
}
