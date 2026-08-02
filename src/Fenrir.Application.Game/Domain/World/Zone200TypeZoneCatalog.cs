using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World;

public static class Zone200TypeZoneCatalog
{
    // Server/ts25zone/S07_MyGame01.cpp:1136-1158 -- mCheckZone200TypeServer switch, TRUE only for these four
    // map numbers, FALSE via an explicit default for every other configured map.
    private static readonly FrozenSet<short> SuppressedExperienceMapNumbers =
        new short[] { 200, 297, 298, 299 }.ToFrozenSet();

    public static bool IsZone200TypeZone(short mapId)
    {
        return SuppressedExperienceMapNumbers.Contains(mapId);
    }
}
