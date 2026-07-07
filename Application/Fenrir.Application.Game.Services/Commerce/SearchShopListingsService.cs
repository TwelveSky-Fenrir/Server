using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Commerce;

/// <remarks>
///     One remaining scope cut vs. S04_MyWork02.cpp:6475's full <c>CHK_SELL_TYPE</c> switch: the
///     item-&gt;category mapping only reproduces the <c>tITEM_INFO-&gt;iSort</c>-keyed first pass
///     (<see cref="SortToCategory" />, ~10 categories, all backed by fields Fenrir already catalogs) -- the
///     secondary <c>IsValidCostume</c> re-check and the huge <c>tITEM_INFO-&gt;iIndex</c> whitelist (hundreds
///     of cash/fish/misc-consumable item-id overrides) are not modeled, matching this codebase's established
///     precedent for uncataloged legacy data (e.g. <c>CraftResolver</c>'s own scope disclaimer). An item
///     whose sort isn't in <see cref="SortToCategory" /> still surfaces under an unfiltered "show all" query
///     (<see cref="IsMatch" />'s default branch, S04_MyWork02.cpp:6681-6909's own default-inclusion rule) --
///     it is genuinely excluded only from a search with an explicit, non-ALL category filter. The legacy
///     catch-all "other" category code itself is not modeled (no citation available), so that one specific
///     explicit-filter case still under-approximates.
///     <para>
///         Both listing pools are aggregated, matching the legacy's own two-part union
///         (S04_MyWork02.cpp:6523-6558 proxy, :6559-6585 personal): every currently open proxy/deputy shop
///         cluster-wide (<see cref="IOfflineShopRepository.GetAllOpenAsync" /> -- the shared database is
///         already the one store every shard's proxy shops persist through, the Fenrir-sharded equivalent of
///         legacy's cross-instance shared-memory table) plus every live personal shop open on this zone
///         instance only (<see cref="Zone.Players" />, ephemeral, gone the instant its owner disconnects --
///         deliberate legacy asymmetry, not a bug).
///     </para>
/// </remarks>
public sealed class SearchShopListingsService(WorldDataCache worldData, IOfflineShopRepository offlineShops)
    : ISearchShopListingsService
{
    private const int TypeAll = 0;
    private const int TypeCommon = 1;
    private const int TypeUnique = 2;
    private const int TypeRare = 3;
    private const int SortAll = 0;
    private const int MaxResults = 1000;

    /// <summary>tITEM_INFO->iSort -> PROXY_ITEM_SORT category (S04_MyWork02.cpp:6636-6679).</summary>
    private static readonly Dictionary<byte, int> SortToCategory = new()
    {
        [5] = 1, // EPSORT_SKILL
        [7] = 2, // EPSORT_NECKLE
        [8] = 3, [29] = 3, // EPSORT_CLOAK
        [6] = 4, [9] = 4, // EPSORT_COSTUM
        [10] = 5, // EPSORT_GLOVES
        [11] = 6, // EPSORT_RING
        [12] = 7, // EPSORT_SHOES
        [13] = 8, [14] = 8, [15] = 8, [16] = 8, [17] = 8, [18] = 8, [19] = 8, [20] = 8, [21] = 8, // EPSORT_WEAPON
        [22] = 17, [28] = 17, [30] = 17 // EPSORT_MOUNT
    };

    public async ValueTask<IReadOnlyList<SearchShopListingsResponse>> SearchAsync(SearchShopListingsRequest packet,
        Zone zone, CancellationToken cancellationToken)
    {
        var cycleTick = unchecked((uint)Environment.TickCount);
        var results = new List<SearchShopListingsResponse>();

        // Proxy/deputy shops first, matching the legacy aggregation order (S04_MyWork02.cpp:6523-6558
        // before :6559-6585) -- the shared MaxResults cap below is enforced across both pools combined, not
        // per-pool, matching the legacy's own single running send-count check.
        var proxyListings = await offlineShops.GetAllOpenAsync(cancellationToken);
        foreach (var row in proxyListings)
        {
            if (results.Count >= MaxResults)
                return results;

            if (!worldData.ItemsById.TryGetValue(row.ItemId, out var itemDefinition) ||
                !IsMatch(packet.Sort1, packet.Sort2, itemDefinition))
                continue;

            var page = row.SlotIndex / PshopPurchasePolicy.MaxSlots;
            var slot = row.SlotIndex % PshopPurchasePolicy.MaxSlots;

            results.Add(new SearchShopListingsResponse
            {
                // characterId * 2 + 1 -- the same proxy-shop wire-UniqueNumber formula
                // OpenShopStallService.Prepare/ProxyShopBroadcastEntry already use for this same shop.
                UniqueNumber = unchecked((uint)(row.CharacterId * 2 + 1)),
                AvatarName = row.AvatarName,
                Page = page,
                Index = slot,
                PshopItemInfo = [row.ItemId, row.Quantity, row.Value, row.SerialNumber, row.Price, 0, 0, 0, 0],
                // OfflineShopItems.SocketData has no established encoding anywhere in this codebase yet --
                // OpenShopStallService always persists it null today, so there is nothing to decode in
                // practice; defaulting to zero here rather than inventing a decode format.
                SocketInfo = [0, 0, 0],
                CycleTick = cycleTick
            });
        }

        foreach (var seller in zone.Players)
        {
            if (results.Count >= MaxResults)
                return results;

            if (!seller.PshopOpen || seller.PshopListing is not { } listing)
                continue;

            for (var page = 0; page < PshopPurchasePolicy.MaxPages && results.Count < MaxResults; page++)
            for (var slot = 0; slot < PshopPurchasePolicy.MaxSlots && results.Count < MaxResults; slot++)
            {
                var view = PshopPurchasePolicy.ReadSlot(listing, page, slot);
                if (!view.IsOccupied)
                    continue;

                if (!worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition) ||
                    !IsMatch(packet.Sort1, packet.Sort2, itemDefinition))
                    continue;

                var socketBase = (page * PshopPurchasePolicy.MaxSlots + slot) * 3;
                results.Add(new SearchShopListingsResponse
                {
                    UniqueNumber = listing.UniqueNumber,
                    AvatarName = seller.Name,
                    Page = page,
                    Index = slot,
                    PshopItemInfo = [view.ItemId, view.Quantity, view.Value, view.Serial, view.Price, 0, 0, 0, 0],
                    SocketInfo =
                    [
                        listing.SocketInfo[socketBase], listing.SocketInfo[socketBase + 1],
                        listing.SocketInfo[socketBase + 2]
                    ],
                    CycleTick = cycleTick
                });
            }
        }

        return results;
    }

    /// <summary>
    ///     tITEM_INFO-&gt;iSort classification (<see cref="SortToCategory" />) plus the legacy's own
    ///     default-inclusion rule for an item neither classification pass resolves
    ///     (S04_MyWork02.cpp:6681-6909): still included under an unfiltered "show all" query. See this
    ///     type's own remarks for the one remaining gap (the numeric "other" catch-all category code).
    /// </summary>
    private static bool IsMatch(int sort1, int sort2, ItemDefinition itemDefinition)
    {
        if (SortToCategory.TryGetValue(itemDefinition.Item.Sort, out var category))
            return Matches(sort1, sort2, category, itemDefinition.Item.Type);

        return sort1 == TypeAll && sort2 == SortAll;
    }

    /// <summary>CHK_SELL_TYPE, minus the goto-fallthrough-to-the-next-switch-table tail (out of scope, see remarks).</summary>
    private static bool Matches(int sort1, int sort2, int category, byte itemType)
    {
        if (sort1 == TypeAll && (sort2 == SortAll || sort2 == category))
            return true;

        if (sort2 != SortAll && sort2 != category)
            return false;

        return sort1 switch
        {
            TypeCommon => itemType == 1,
            TypeUnique => itemType == 2,
            TypeRare => itemType == 3,
            _ => false
        };
    }
}
