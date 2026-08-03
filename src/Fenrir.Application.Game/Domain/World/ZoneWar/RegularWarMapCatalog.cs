using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct RegularWarMapConfig(
    short MapId,
    byte WarSlotIndex,
    bool IsBossWar,
    bool AnnouncesSmallestPresentTribe);

public static class RegularWarMapCatalog
{
    public static readonly ImmutableArray<RegularWarMapConfig> ConfiguredMaps = BuildConfiguredMaps();

    public static bool TryGet(short mapId, out RegularWarMapConfig config)
    {
        foreach (var candidate in ConfiguredMaps)
            if (candidate.MapId == mapId)
            {
                config = candidate;
                return true;
            }

        config = default;
        return false;
    }

    private static ImmutableArray<RegularWarMapConfig> BuildConfiguredMaps()
    {
        ReadOnlySpan<short> servers = [49, 146, 149, 154, 157, 160, 120, 121, 122, 295, 296];

        var builder = ImmutableArray.CreateBuilder<RegularWarMapConfig>(servers.Length);
        for (byte slot = 0; slot < servers.Length; slot++)
        {
            var mapId = servers[slot];

            builder.Add(new RegularWarMapConfig(
                mapId,
                slot,
                mapId == 295,
                mapId == 160));
        }

        return builder.MoveToImmutable();
    }
}
