using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.AntiCheat;

public static class PotionWhileAttackingZoneWhitelist
{
    private static readonly FrozenSet<int> ListedZones = new[]
    {
        1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 13, 14, 38, 55, 75, 85, 86, 87, 89, 90,
        99, 100, 125, 140, 141, 142, 143, 195, 196, 197, 198, 199, 201, 270, 271, 272, 273, 274, 295, 296
    }.ToFrozenSet();

        public static int Count => ListedZones.Count;

        public static bool IsListed(int zoneId)
    {
        return ListedZones.Contains(zoneId);
    }
}
