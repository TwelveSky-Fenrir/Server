using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The 4 legacy server numbers that run the Valley of the Deceased (<c>Process_Zone_200</c>) schedule --
///     distinct from <see cref="RegularWarMapCatalog" />'s own 11 Zone049 maps.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1116-1137 (eligibility flag set only for these four server
///     numbers) ; Server/ts25zone/H07_MyGame.h:36 (guarding macro is live/compiled).
/// </remarks>
public static class ValleyWarMapCatalog
{
    public static readonly ImmutableArray<short> ConfiguredMaps = [200, 297, 298, 299];

    public static bool Contains(short mapId)
    {
        return ConfiguredMaps.Contains(mapId);
    }
}
