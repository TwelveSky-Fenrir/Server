using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed record Zone195NokSanSite(
    short MapId,
    int StoneSlotIndex,
    short LegacyServerNumber,
    float PostX = Zone195NokSanState.DefaultPostX,
    float PostZ = Zone195NokSanState.DefaultPostZ,
    float CaptureRadius = Zone195NokSanState.DefaultCaptureRadius)
{
    public bool IsRewardWindowShard => StoneSlotIndex == 0;
}

public sealed class Zone195NokSanSiteCatalog
{
    public static readonly Zone195NokSanSiteCatalog Empty = new([]);

    public static readonly Zone195NokSanSiteCatalog Legacy = new(
    [
        new Zone195NokSanSite(196, 0, 196)
    ]);

    private readonly FrozenDictionary<short, Zone195NokSanSite> _byMapId;

    public Zone195NokSanSiteCatalog(IEnumerable<Zone195NokSanSite> sites)
    {
        _byMapId = sites.ToFrozenDictionary(static site => site.MapId);
    }

    public bool TryGet(short mapId, out Zone195NokSanSite? site)
    {
        return _byMapId.TryGetValue(mapId, out site);
    }
}
