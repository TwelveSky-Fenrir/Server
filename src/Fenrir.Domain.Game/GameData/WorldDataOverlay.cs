using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Domain.Game.GameData;

internal static class WorldDataOverlay
{
    private static readonly FrozenSet<int> StackableItemIds = new[]
    {
        501, 502, 503, 504, 505, 506, 507, 508, 509, 512, 536, 539, 553, 558, 566, 567, 574, 576, 578, 579,
        582, 583, 584, 586, 592, 593, 601, 602, 610, 611, 612, 613, 626, 627, 628, 632, 633, 635, 636, 637,
        638, 639, 640, 641, 649, 650, 652, 658, 664, 665, 666, 667, 686, 687, 691, 692, 693, 694, 695, 696,
        698, 699, 722, 724, 800, 801, 802, 803, 804, 805, 806, 807, 824, 828, 829, 837, 864, 867, 889, 984,
        1017, 1018, 1019, 1020, 1021, 1022, 1023, 1024, 1025, 1027, 1035, 1036, 1037, 1041, 1043, 1045,
        1047, 1048, 1049, 1066, 1092, 1093, 1097, 1098, 1101, 1102, 1103, 1108, 1118, 1119, 1120, 1121,
        1122, 1123, 1124, 1126, 1129, 1130, 1132, 1140, 1141, 1145, 1146, 1147, 1148, 1149, 1150, 1151,
        1152, 1153, 1154, 1155, 1163, 1166, 1167, 1171, 1178, 1179, 1186, 1187, 1188, 1190, 1191, 1192,
        1193, 1194, 1195, 1196, 1197, 1198, 1199, 1200, 1201, 1211, 1214, 1215, 1216, 1217, 1219, 1221,
        1222, 1225, 1226, 1227, 1228, 1231, 1232, 1233, 1234, 1235, 1236, 1237, 1240, 1241, 1243, 1356,
        1357, 1359, 1370, 1378, 1379, 1421, 1422, 1434, 1436, 1437, 1438, 1439, 1444, 1447, 1448, 1449,
        1453, 1454, 1455, 1456, 1457, 1458, 1489, 1490, 1491, 1492, 1494, 1499, 1878, 2019, 2020, 2021,
        2101, 2138, 2150, 2159, 2193, 2249, 2292, 2336, 2337, 2345, 2376, 2390, 2394, 2395, 2397, 2462,
        2477, 7101, 7102, 7103, 7104, 7105, 7106, 7107, 7108, 8000, 8001, 8002, 8003, 8004, 8005, 8101,
        8102, 8103, 8104, 8105, 8106, 8107, 8108, 8110, 8111, 8112, 8113, 8114, 8115, 8401, 8402, 8403,
        8404, 8405, 8406, 8407, 8408, 8409, 8410, 8411, 8412, 8413, 8414, 8415, 8416, 8417, 8418, 8419,
        8420, 8421, 8422, 8423, 8425, 8426, 8427, 8428, 8429, 8430, 8431, 8432, 8433, 8434, 8435, 8436,
        8437, 8438, 8439, 17026, 17027, 17028, 17029, 17035, 17037, 17038, 17039, 17040, 17041, 17042,
        17043, 17124, 76542, 76543, 76544, 92291, 92296, 92297, 92298, 99101, 99406, 99407, 99408, 99409
    }.ToFrozenSet();

    private static readonly FrozenDictionary<int, short> PetModelByItemId = new Dictionary<int, short>
    {
        [76000] = 69,
        [76001] = 70,
        [76002] = 71,
        [76003] = 72,
        [76004] = 73,
        [76005] = 74,
        [76006] = 75,
        [76007] = 76
    }.ToFrozenDictionary();

    public static ImmutableArray<ItemRowDto> ApplyItems(IReadOnlyList<ItemRowDto> items)
    {
        var result = ImmutableArray.CreateBuilder<ItemRowDto>(items.Count);

        foreach (var item in items)
        {
            var adjusted = StackableItemIds.Contains(item.ItemId)
                ? item with { Sort = 99 }
                : item;

            if (PetModelByItemId.TryGetValue(adjusted.ItemId, out var modelNumber))
                adjusted = adjusted with { Sort = 28, DataNumber3D = modelNumber };

            result.Add(adjusted);
        }

        return result.ToImmutable();
    }

    public static ImmutableArray<ZoneNpcSpawnRowDto> ApplyNpcSpawns(IReadOnlyList<ZoneNpcSpawnRowDto> npcSpawns)
    {
        var result = ImmutableArray.CreateBuilder<ZoneNpcSpawnRowDto>(npcSpawns.Count);

        foreach (var npcSpawn in npcSpawns)
            if (npcSpawn.ZoneNumber != 124)
                result.Add(npcSpawn);

        return result.ToImmutable();
    }
}
