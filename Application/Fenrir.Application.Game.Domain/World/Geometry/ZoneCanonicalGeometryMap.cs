using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.Geometry;

/// <summary>
///     Physical-&gt;canonical map-id remap for <c>.WM</c> geometry loading -- a faithful port of the
///     <c>switch (mSERVER_INFO.mServerNumber)</c> in legacy <c>WORLD_FOR_GXD::LoadWM</c>
///     (<c>Server/ts25zone/S09_MyWorld.cpp:96-368</c>). Dungeon/instance/war variants share one canonical
///     mesh, so e.g. physical map 336 loads <c>Z310.WM</c> and every Labyrinth instance loads <c>Z175.WM</c>.
///     Without this, those maps look up a per-map <c>Z{mapId:D3}.WM</c> that does not exist on disk and fall
///     back to unconstrained movement (see <see cref="Zone" />'s geometry-load path).
/// </summary>
/// <remarks>
///     The shipped build compiles with <c>LNW33</c> defined (<c>M33</c> transitively defines it,
///     <c>Server/Header/Protocol/DEFINE.h:21-23</c>), so the <c>#ifdef LNW33</c> branch mapping
///     39/144/145/313 -&gt; 126 (<c>S09_MyWorld.cpp:305-313</c>) is used, NOT the compiled-out
///     <c>#ifndef LNW33</c> alternative that would map 39/144/145 -&gt; 39 (<c>:315-322</c>).
///     The <c>.WM</c> filename is <c>Z%03d.WM</c> with NO <c>_FIX</c> suffix (<c>:369</c>) -- the <c>_FIX</c>
///     prefix applies only to the spawn <c>.WREGION</c> filename in <c>MySummon</c>, never to geometry.
///     This is a distinct remap from the spawn-side <c>mSameSummon</c> table (<c>S10_MySummon.cpp:88-360</c>),
///     which diverges from this one for some ids; spawn regions are DB-backed by physical zone and are not
///     affected here. Identity mappings (physical == canonical) are omitted:
///     <see cref="ResolveCanonicalMapId" /> returns its input unchanged when the id is absent.
/// </remarks>
public static class ZoneCanonicalGeometryMap
{
    private static readonly FrozenDictionary<short, short> Redirects = Build();

    /// <summary>
    ///     Returns the canonical map id whose <c>Z{canonical:D3}.WM</c> backs <paramref name="mapId" />'s geometry,
    ///     or <paramref name="mapId" /> itself when it is its own canonical (the legacy switch's <c>default</c>).
    /// </summary>
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

        // S09_MyWorld.cpp:98-138 -- every Labyrinth (G/M/M33/L) instance shares Z175.WM.
        Group(175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189,
            190, 191, 192, 193, 19, 20, 21, 34, 25, 26, 27, 35, 31, 32, 33, 36);
        Group(16, 22, 28); // :140-145
        Group(17, 23, 29); // :147-152
        Group(18, 24, 30); // :154-159
        Group(40, 41, 42); // :161-166
        Group(43, 44, 45); // :168-173
        Group(46, 47, 48); // :175-180
        Group(56, 57, 58); // :182-187
        Group(59, 60, 61); // :189-194
        Group(62, 65, 68); // :196-201
        Group(63, 66, 69); // :203-208
        Group(64, 67, 70); // :210-215
        Group(76, 77, 78, 79); // :217-223
        Group(80, 81, 82, 83); // :225-231
        Group(101, 102, 103, 167); // :233-239
        Group(104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 168, 169,
            251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261, 262, 263, 264, 265, 266); // :241-275
        Group(126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 171, 172, 173, 174,
            210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221,
            39, 144, 145, 313); // :277-313 (LNW33 branch: 39/144/145/313 -> 126)
        Group(222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233); // :324-338
        Group(154, 120, 121, 122, 295, 296, 164, 157, 160); // :340-350
        Group(195, 85, 86, 87, 99, 100, 196, 197, 198, 199); // :352-363
        Group(310, 336); // :364-367

        return map.ToFrozenDictionary();
    }
}
