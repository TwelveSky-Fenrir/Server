using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Commerce;

/// <summary>
///     C21a -- <c>IsValidCostume</c>, the secondary <c>tITEM_INFO-&gt;iIndex</c> whitelist check the
///     proxy/personal-shop item-search filter (<c>CZ_PSHOP_ITEM_INFO_SEND</c>) applies to a candidate item
///     AFTER its primary <c>iSort</c>-&gt;category switch has already fallen through for that item -- a
///     genuinely independent second classification pass, not a fallback for a missing category value. An
///     item can therefore surface under a "Costume" category search even though its own declared
///     <c>iSort</c> maps to something unrelated (or to nothing the primary switch classifies at all).
/// </summary>
/// <remarks>
///     Réf. contract : C21-commerce-social.md §A (Edge cases A), sourced from
///     <c>Server/ts25zone/S04_MyWork02.cpp:6636-6684</c> and
///     <c>Server/Header/function.h:1798-2008,1955-1957,1997-1998,2002-2004</c>. The whitelist is a hardcoded
///     set of exact item-identifier values, not a range check against the item's own category field: a
///     contiguous block 301-402 (102 ids), plus discrete ids/ranges 2146-2148, 1801-1803, 1891-1893,
///     17701-17703, 18124-18132, 93301-93330, 93334-93345, 93376-93381, 93385-93405, and 76524-76526. The
///     gaps between these ranges (93331-93333, 93382-93384, and everything outside the listed spans) are
///     deliberate exclusions confirmed by the contract, not omissions -- do not "round up" any range to close
///     a gap.
///     <para>
///         Wiring this into the existing category-match fallthrough
///         (<c>Fenrir.Application.Game.Services.Commerce.SearchShopListingsService.IsMatch</c>) is deferred
///         to a serial integration pass -- see this change's wiring manifest -- since this class was added as
///         a new file rather than editing that service directly (concurrent sibling waves touch the same
///         file family).
///     </para>
/// </remarks>
public static class CostumeSearchWhitelist
{
    /// <summary>
    ///     The <c>PROXY_ITEM_SORT</c> category value the primary switch already assigns to raw item sorts
    ///     6/9 ("EPSORT_COSTUM",
    ///     <c>Fenrir.Application.Game.Services.Commerce.SearchShopListingsService.SortToCategory</c>) -- this
    ///     secondary whitelist pass resolves to the SAME category value, so a whitelisted item is filtered
    ///     exactly like a directly-sorted costume item once matched (the common/unique/rare item-type
    ///     sub-filter still applies on top, matching every other classified item's own treatment).
    /// </summary>
    public const int CostumeCategory = 4;

    /// <summary>
    ///     Every <c>tITEM_INFO-&gt;iIndex</c> (item type identifier) <c>IsValidCostume</c> treats as a
    ///     costume, regardless of the item's own <c>iSort</c>. See this type's own remarks for the exact
    ///     ranges and the citation backing each.
    /// </summary>
    public static readonly FrozenSet<int> ItemIds = BuildWhitelist();

    /// <summary>True when <paramref name="itemId" /> (the item's <c>iIndex</c>) is on the costume whitelist.</summary>
    public static bool Contains(int itemId)
    {
        return ItemIds.Contains(itemId);
    }

    private static FrozenSet<int> BuildWhitelist()
    {
        var ids = new List<int>(195);
        ids.AddRange(Enumerable.Range(301, 102)); // 301-402
        ids.AddRange(Enumerable.Range(2146, 3)); // 2146-2148
        ids.AddRange(Enumerable.Range(1801, 3)); // 1801-1803
        ids.AddRange(Enumerable.Range(1891, 3)); // 1891-1893
        ids.AddRange(Enumerable.Range(17701, 3)); // 17701-17703
        ids.AddRange(Enumerable.Range(18124, 9)); // 18124-18132
        ids.AddRange(Enumerable.Range(93301, 30)); // 93301-93330 (93316/93317 re-added, 93331/93332 excluded)
        ids.AddRange(Enumerable.Range(93334, 12)); // 93334-93345 (93331-93333 excluded, see the range above)
        ids.AddRange(Enumerable.Range(93376, 6)); // 93376-93381
        ids.AddRange(Enumerable.Range(93385, 21)); // 93385-93405 (93382-93384 excluded)
        ids.AddRange(Enumerable.Range(76524, 3)); // 76524-76526
        return ids.ToFrozenSet();
    }
}
