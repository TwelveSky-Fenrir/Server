using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World;

public static class Zone200TypeZoneCatalog
{
    private static readonly FrozenSet<short> SuppressedExperienceMapNumbers =
        new short[] { 200, 297, 298, 299 }.ToFrozenSet();

    public static bool IsZone200TypeZone(short mapId)
    {
        return SuppressedExperienceMapNumbers.Contains(mapId);
    }
}
