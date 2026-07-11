using System.Collections.Frozen;

namespace Fenrir.Application.Game.GameData;

public readonly record struct SpawnRegionCanonicalZone(short CanonicalZoneId, bool UsesFixSuffix);

public static class ZoneCanonicalSpawnRegionMap
{

        private static readonly FrozenSet<short> FixSuffixZones =
        new short[] { 39, 144, 145, 313, 74 }.ToFrozenSet();

    private static readonly FrozenDictionary<short, short> CanonicalRedirects = BuildCanonicalRedirects();

        public static short ResolveCanonicalSpawnZoneId(short physicalZoneId)
    {
        return CanonicalRedirects.GetValueOrDefault(physicalZoneId, physicalZoneId);
    }

        public static bool UsesFixSuffix(short physicalZoneId)
    {
        return FixSuffixZones.Contains(physicalZoneId);
    }

        public static SpawnRegionCanonicalZone Resolve(short physicalZoneId)
    {
        return new SpawnRegionCanonicalZone(ResolveCanonicalSpawnZoneId(physicalZoneId), UsesFixSuffix(physicalZoneId));
    }

    private static FrozenDictionary<short, short> BuildCanonicalRedirects()
    {
        var map = new Dictionary<short, short>();

        void Group(short canonical, params short[] physicals)
        {
            foreach (var physical in physicals)
                map[physical] = canonical;
        }

        Group(16, 22, 28);
        Group(17, 23, 29);
        Group(18, 24, 30);

        Group(101, 102, 103, 167);

        Group(126, 130, 134, 171);
        Group(127, 131, 135, 172);
        Group(128, 132, 136, 173);
        Group(129, 133, 137, 174);

        Group(310, 336);



        return map.ToFrozenDictionary();
    }
}
