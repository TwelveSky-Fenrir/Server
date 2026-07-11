using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.Geometry;

public static class ZoneCanonicalGeometryMap
{
    private static readonly FrozenDictionary<short, short> Redirects = Build();

        public static short ResolveCanonicalMapId(short mapId)
    {
        return Redirects.GetValueOrDefault(mapId, mapId);
    }

    private static FrozenDictionary<short, short> Build()
    {
        var map = new Dictionary<short, short>();

        void Group(short canonical, params short[] physicals)
        {
            foreach (var physical in physicals)
                map[physical] = canonical;
        }

        Group(175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189,
            190, 191, 192, 193, 19, 20, 21, 34, 25, 26, 27, 35, 31, 32, 33, 36);
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
        Group(76, 77, 78, 79);
        Group(80, 81, 82, 83);
        Group(101, 102, 103, 167);
        Group(104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 168, 169,
            251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261, 262, 263, 264, 265, 266);
        Group(126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 171, 172, 173, 174,
            210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221,
            39, 144, 145, 313);
        Group(222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233);
        Group(154, 120, 121, 122, 295, 296, 164, 157, 160);
        Group(195, 85, 86, 87, 99, 100, 196, 197, 198, 199);
        Group(310, 336);

        return map.ToFrozenDictionary();
    }
}
