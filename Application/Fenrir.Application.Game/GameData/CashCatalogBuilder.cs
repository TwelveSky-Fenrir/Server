using System.Collections.Immutable;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     Pure port of <c>MyDB::GetItemMall</c> (Server/ts25extra/S08_MyDB.cpp:137-235, verified byte-for-bit)
///     -- builds the cash-shop's two derived views from world.ItemMallProducts: a flat 800-slot
///     "cost info" master table (<see cref="CostInfoByIndex" />, indexed by the wire's own
///     <c>CostInfoIndex</c> -- ANTI-CHEAT: a purchase resolves price/item from THIS, never from the
///     client-submitted <c>Value[]</c> array) and the 4x20x10x4 flattened display grid
///     (<see cref="DisplayGrid" />, ZC_GET_CASH_ITEM_INFO_RECV.CashItemInfo). Computed ONCE at boot
///     (<see cref="WorldDataCacheBuilder" />) since world.ItemMallProducts is boot-time-static reference
///     data in this pass (no live catalog reload/admin-edit path exists yet).
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
    ///     One <c>mCostInfoValue[costInfoIndex]</c> row: [Cost, ItemId, Quantity, Type]. Default (all zero)
    ///     for an index never assigned to a real product. <see cref="ItemMallProductId" /> is a Fenrir-only
    ///     addition (the legacy's own in-memory row does not carry it either, verified) purely so
    ///     game.CashLog's audit trail can reference the real catalog row rather than this runtime-computed
    ///     position -- never sent on the wire.
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
        ///     verbatim. -1 for an unfilled/inactive display slot (matches the legacy's own FillMemory(...,-1)
        ///     init). A plain <c>int[]</c> (not <see cref="ImmutableArray{T}" />), safe to hand out and reuse
        ///     as-is across every request without a defensive copy: built ONCE at boot and never mutated
        ///     afterwards (same "read-mostly reference data" trust every other <see cref="WorldDataCache" />
        ///     field already carries), and the 12 809-byte wire response this feeds is otherwise the single
        ///     largest per-request allocation in the whole protocol.
        /// </summary>
        public required int[] DisplayGrid { get; init; }
    }

    /// <summary>
    ///     <paramref name="products" /> need not be pre-sorted -- this orders by ItemMallProductId ascending
    ///     within each ProductType itself (the legacy's own "ORDER BY Number ASC, Cost ASC" query, where
    ///     Number IS ItemMallProductId and is already unique, making "Cost ASC" a moot tie-breaker never
    ///     actually exercised). Only ProductType 1..4 participate (5 is the version-sentinel row, excluded
    ///     from the catalog itself -- <see cref="ResolveVersion" />).
    /// </summary>
    public static CashCatalog Build(IEnumerable<ItemMallProductRowDto> products)
    {
        var costInfo = new CostInfoEntry[MaxCashNum];
        var grid = new int[MaxCashType * MaxCashPage * MaxCashItemPerPage * MaxCashItemDetail];
        Array.Fill(grid, -1);

        // rowCount mirrors the legacy's own `iRows` (S08_MyDB.cpp's FIRST pass): advances for EVERY fetched
        // row of this type, independent of the [1,99999] ItemID validity check below -- this drives
        // costInfoIndex/CostInfoByIndex, which the legacy's first pass populates unconditionally.
        // pageCount/itemCount is the SEPARATE display cursor (the legacy's SECOND pass) -- it only advances
        // for a row that passes the ItemID range check (verified S08_MyDB.cpp:182-185: an out-of-range
        // ItemID `continue`s BEFORE the type/showType validation that itemCount++ follows), so an
        // out-of-range row consumes its own costInfoIndex slot but leaves no gap in the display grid.
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
