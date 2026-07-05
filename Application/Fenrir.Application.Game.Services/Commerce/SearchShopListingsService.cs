using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Commerce;

/// <remarks>
///     Two scope cuts vs. S04_MyWork02.cpp:6475's full <c>CHK_SELL_TYPE</c> switch:
///     (1) offline/deputy (proxy) shop listings are not included -- <see cref="Data.Commerce.IOfflineShopRepository" />
///     only supports a per-character lookup, not "every currently open shop", and adding that is a bigger
///     prerequisite than this pass covers; (2) the item-&gt;category mapping only reproduces the
///     <c>tITEM_INFO-&gt;iSort</c>-keyed switch (<see cref="SortToCategory" />, ~10 categories, all backed by
///     fields Fenrir already catalogs) -- the secondary <c>IsValidCostume</c> re-check and the huge
///     <c>tITEM_INFO-&gt;iIndex</c> whitelist (hundreds of cash/fish/misc-consumable item-id overrides) are not
///     modeled, matching this codebase's established precedent for uncataloged legacy data (e.g.
///     <c>CraftResolver</c>'s own scope disclaimer). An item whose sort isn't in <see cref="SortToCategory" /> is
///     silently excluded from every search, including "show all" -- an under-approximation of the legacy (which
///     drops it too, unless the iIndex table happens to catalog it), never an over-approximation.
/// </remarks>
public sealed class SearchShopListingsService(WorldDataCache worldData) : ISearchShopListingsService
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

    public IReadOnlyList<SearchShopListingsResponse> Search(SearchShopListingsRequest packet, Zone zone)
    {
        var cycleTick = unchecked((uint)Environment.TickCount);
        var results = new List<SearchShopListingsResponse>();

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

                if (!worldData.ItemsById.TryGetValue(view.ItemId, out var itemDefinition))
                    continue;

                if (!SortToCategory.TryGetValue(itemDefinition.Item.Sort, out var category))
                    continue;

                if (!Matches(packet.Sort1, packet.Sort2, category, itemDefinition.Item.Type))
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
