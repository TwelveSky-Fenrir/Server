using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class SearchShopListingsService(
    WorldDataCache worldData,
    IOfflineShopRepository offlineShops,
    ILogger<SearchShopListingsService> logger) : ISearchShopListingsService
{
    private const int TypeAll = 0;
    private const int TypeCommon = 1;
    private const int TypeUnique = 2;
    private const int TypeRare = 3;
    private const int SortAll = 0;
    private const int MaxResults = 1000;

    private const byte ExcludedRawSort = 3;

    private static readonly Dictionary<byte, int> SortToCategory = new()
    {
        [5] = 1,
        [7] = 2,
        [8] = 3, [29] = 3,
        [6] = 4, [9] = 4,
        [10] = 5,
        [11] = 6,
        [12] = 7,
        [13] = 8, [14] = 8, [15] = 8, [16] = 8, [17] = 8, [18] = 8, [19] = 8, [20] = 8, [21] = 8,
        [22] = 17, [28] = 17, [30] = 17
    };

    public async ValueTask<IReadOnlyList<SearchShopListingsResponse>> SearchAsync(SearchShopListingsRequest packet,
        Zone zone, CancellationToken cancellationToken)
    {
        var cycleTick = unchecked((uint)Environment.TickCount);
        var results = new List<SearchShopListingsResponse>();

        var proxyListings = await offlineShops.GetAllOpenAsync(cancellationToken);
        foreach (var row in proxyListings)
        {
            if (results.Count >= MaxResults)
            {
                logger.LogDebug(
                    "Search shop listings: result cap ({MaxResults}) reached while scanning proxy listings",
                    MaxResults);
                return results;
            }

            if (!worldData.ItemsById.TryGetValue(row.ItemId, out var itemDefinition) ||
                !IsMatch(packet.Sort1, packet.Sort2, itemDefinition))
                continue;

            var page = row.SlotIndex / PshopPurchasePolicy.MaxSlots;
            var slot = row.SlotIndex % PshopPurchasePolicy.MaxSlots;

            results.Add(new SearchShopListingsResponse
            {
                UniqueNumber = unchecked((uint)(row.CharacterId * 2 + 1)),
                AvatarName = row.AvatarName,
                Page = page,
                Index = slot,
                PshopItemInfo = [row.ItemId, row.Quantity, row.Value, row.SerialNumber, row.Price, 0, 0, 0, 0],
                SocketInfo = [0, 0, 0],
                CycleTick = cycleTick
            });
        }

        foreach (var seller in zone.Players)
        {
            if (results.Count >= MaxResults)
            {
                logger.LogDebug(
                    "Search shop listings: result cap ({MaxResults}) reached while scanning live personal shops",
                    MaxResults);
                return results;
            }

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

    private static bool IsMatch(int sort1, int sort2, ItemDefinition itemDefinition)
    {
        if (itemDefinition.Item.Sort == ExcludedRawSort)
            return false;

        if (SortToCategory.TryGetValue(itemDefinition.Item.Sort, out var category))
            return Matches(sort1, sort2, category, itemDefinition.Item.Type);

        if (CostumeSearchWhitelist.Contains(itemDefinition.Item.ItemId))
            return Matches(sort1, sort2, CostumeSearchWhitelist.CostumeCategory, itemDefinition.Item.Type);

        return sort1 == TypeAll && sort2 == SortAll;
    }

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
