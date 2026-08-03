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
    private const int SortOther = 19;
    private const int MaxResults = 1000;

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

    private static readonly Dictionary<int, int> ItemIdToCategory = BuildItemIdToCategory();

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

    private static Dictionary<int, int> BuildItemIdToCategory()
    {
        var map = new Dictionary<int, int>(163);

        foreach (var itemId in (int[])[1048, 1049])
            map[itemId] = 13;

        foreach (var itemId in (int[])
                 [
                     1101, 1102, 1108, 1166, 1237, 1167, 1130, 829, 1201, 1124,
                     1200, 1145, 1190, 1221, 1436, 1438, 1439, 1118, 1454, 1119,
                     1456, 1120, 1163, 1186, 1228, 2345, 1132, 1133, 1155, 1171,
                     1214, 1211, 8100, 593, 1218, 1103, 1455, 1358, 1126, 2138,
                     2292, 1231, 1146, 1147, 1148, 1232, 1149, 1150, 1151, 1233,
                     1152, 1153, 1154, 1134, 1135, 1136, 1142, 1459, 1137, 1138,
                     1139, 1143, 2022, 8000, 8001, 8002, 8003
                 ])
            map[itemId] = 18;

        foreach (var itemId in (int[])[582, 583, 584, 586])
            map[itemId] = 12;

        foreach (var itemId in (int[])
                 [
                     633, 619, 825, 538, 540, 551, 565, 1019, 1020, 1021,
                     1022, 1023, 1422, 1243, 1437, 1457, 695, 696, 698, 2397,
                     826, 828, 576, 699, 824, 501, 502, 503, 504, 8101,
                     8102, 724
                 ])
            map[itemId] = 14;

        foreach (var itemId in (int[])[1024, 1025, 1178, 1179])
            map[itemId] = 9;

        foreach (var itemId in (int[])
                 [
                     1017, 1018, 1092, 1093, 578, 579, 611, 612, 652, 1491,
                     1492, 649, 650, 1489, 1490
                 ])
            map[itemId] = 16;

        foreach (var itemId in (int[])
                 [
                     1301, 1304, 1307, 1302, 1305, 1308, 1303, 1306, 1309, 1313,
                     1314, 1315, 1317, 1318, 1319, 1320, 1321, 1322, 1323, 1324,
                     1325, 1326, 1327, 1328, 1329, 1330, 1331, 1316
                 ])
            map[itemId] = 17;

        foreach (var itemId in (int[])[17124, 99011, 99012, 99013, 1447, 1448, 1449, 1045, 1037, 1038, 1039])
            map[itemId] = 11;

        return map;
    }

    private static bool IsMatch(int sort1, int sort2, ItemDefinition itemDefinition)
    {
        if (SortToCategory.TryGetValue(itemDefinition.Item.Sort, out var category))
            return Matches(sort1, sort2, category, itemDefinition.Item.Type);

        if (CostumeSearchWhitelist.Contains(itemDefinition.Item.ItemId))
            return Matches(sort1, sort2, CostumeSearchWhitelist.CostumeCategory, itemDefinition.Item.Type);

        if (ItemIdToCategory.TryGetValue(itemDefinition.Item.ItemId, out var itemIdCategory))
            return Matches(sort1, sort2, itemIdCategory, itemDefinition.Item.Type);

        return sort1 == TypeAll && (sort2 == SortAll || sort2 == SortOther);
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
