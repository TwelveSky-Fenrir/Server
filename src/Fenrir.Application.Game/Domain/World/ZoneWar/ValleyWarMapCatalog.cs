using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum ValleyWarCampaignKey : byte
{
    Zone200 = 1
}

public static class ValleyWarMapCatalog
{
    public const short CoordinatorMapId = 200;

    public static readonly ImmutableArray<short> ConfiguredMaps = [200, 297, 298, 299];

    public static bool Contains(short mapId)
    {
        return ConfiguredMaps.Contains(mapId);
    }

    public static bool IsCoordinator(short mapId)
    {
        return mapId == CoordinatorMapId;
    }

    public static bool TryGetCampaignKey(short mapId, out ValleyWarCampaignKey campaignKey)
    {
        if (Contains(mapId))
        {
            campaignKey = ValleyWarCampaignKey.Zone200;
            return true;
        }

        campaignKey = default;
        return false;
    }
}
