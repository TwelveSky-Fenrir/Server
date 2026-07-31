using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class ValleyWarMapCatalog
{
    public static readonly ImmutableArray<short> ConfiguredMaps = [200, 297, 298, 299];

    public static bool Contains(short mapId)
    {
        return ConfiguredMaps.Contains(mapId);
    }
}
