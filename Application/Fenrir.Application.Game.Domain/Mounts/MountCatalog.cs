using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountCatalog
{

        public const int GiftEventMinId = 8301;

        public const int GiftEventMaxId = 8331;

        public const int Puma3Id = 1331;

        private static readonly FrozenSet<int> StandaloneIds = new[] { 559 }.ToFrozenSet();

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
               || IsTier3Mount(itemId)
               || itemId == Puma3Id;
    }
}
