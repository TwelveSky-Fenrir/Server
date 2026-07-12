using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountCatalog
{
    public const int GiftEventMinId = 8301;

    public const int GiftEventMaxId = 8331;

    public const int Puma1Id = 1329;

    public const int Puma2Id = 1330;

    public const int Puma3Id = 1331;

    public const int ChristmasId = 1316;

    public const int WornMountSlotThreshold = 10;

    public const int NoActiveMountItemId = 0;

    private static readonly FrozenSet<int> StandaloneIds = new[] { 559 }.ToFrozenSet();

    private static readonly FrozenSet<int> Tier1Ids = new[]
    {
        1301,
        1302,
        1303,
        1313,
        1317,
        1320,
        1323,
        1326
    }.ToFrozenSet();

    private static readonly FrozenSet<int> Tier2Ids = new[]
    {
        1304,
        1305,
        1306,
        1314,
        1318,
        1321,
        1324,
        1327
    }.ToFrozenSet();

    private static readonly FrozenSet<int> Tier3Ids = new[]
    {
        1307,
        1308,
        1309,
        1315,
        1319,
        1322,
        1325,
        1328
    }.ToFrozenSet();

    public static bool IsGiftEventMount(int itemId)
    {
        return itemId is >= GiftEventMinId and <= GiftEventMaxId;
    }

    public static bool IsTier1Mount(int itemId)
    {
        return Tier1Ids.Contains(itemId);
    }

    public static bool IsTier2Mount(int itemId)
    {
        return Tier2Ids.Contains(itemId);
    }

    public static bool IsTier3Mount(int itemId)
    {
        return Tier3Ids.Contains(itemId);
    }

    public static bool IsRecognizedMount(int itemId)
    {
        return StandaloneIds.Contains(itemId)
               || itemId is >= 1332 and <= 1341
               || IsGiftEventMount(itemId)
               || itemId is >= 19002 and <= 19011
               || IsTier1Mount(itemId)
               || IsTier2Mount(itemId)
               || IsTier3Mount(itemId)
               || itemId == Puma1Id
               || itemId == Puma2Id
               || itemId == Puma3Id
               || itemId == ChristmasId;
    }

    public static int ResolveActiveMountItemId(int storedMountItemId, int storedSlotMarker)
    {
        if (storedSlotMarker < WornMountSlotThreshold)
            return NoActiveMountItemId;

        return IsRecognizedMount(storedMountItemId) ? storedMountItemId : NoActiveMountItemId;
    }

    public static int ResolveDisplayedSlotMarker(int storedMountItemId, int storedSlotMarker)
    {
        if (storedSlotMarker < WornMountSlotThreshold)
            return storedSlotMarker;

        return ResolveActiveMountItemId(storedMountItemId, storedSlotMarker) == NoActiveMountItemId
            ? storedSlotMarker - WornMountSlotThreshold
            : storedSlotMarker;
    }
}
