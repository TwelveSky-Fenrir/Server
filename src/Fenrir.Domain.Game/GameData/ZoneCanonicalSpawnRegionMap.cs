using System.Collections.Frozen;

namespace Fenrir.Domain.Game.GameData;

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

        Group(40, 41, 42);
        Group(43, 44, 45);
        Group(46, 47, 48);
        Group(56, 57, 58);
        Group(59, 60, 61);
        Group(62, 65, 68);
        Group(63, 66, 69);
        Group(64, 67, 70);

        Group(101, 102, 103, 167);

        Group(104, 106, 108, 168);
        Group(105, 107, 109, 169);
        Group(110, 112, 114, 116);
        Group(111, 113, 115, 117);

        Group(251, 253, 255, 257);
        Group(252, 254, 256, 258);
        Group(259, 261, 263, 265);
        Group(260, 262, 264, 266);

        Group(126, 130, 134, 171);
        Group(127, 131, 135, 172);
        Group(128, 132, 136, 173);
        Group(129, 133, 137, 174);

        Group(175, 176, 177);
        Group(178, 179, 180, 181);
        Group(182, 183, 184, 185);
        Group(186, 187, 188, 189);
        Group(190, 191, 192, 193);
        Group(19, 20, 21, 34);
        Group(25, 26, 27, 35);
        Group(31, 32, 33, 36);

        Group(210, 213, 216, 219);
        Group(211, 214, 217, 220);
        Group(212, 215, 218, 221);

        Group(222, 225, 228, 231);
        Group(223, 226, 229, 232);
        Group(224, 227, 230, 233);

        Group(310, 336);

        return map.ToFrozenDictionary();
    }
}
