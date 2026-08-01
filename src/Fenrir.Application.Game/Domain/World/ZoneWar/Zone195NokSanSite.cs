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

    // Server/ts25zone/S07_MyGame01.cpp:1174-1196 - switch(mServerNumber): 99 -> slot 2, 100 -> slot 3, 196 -> slot 0.
    // Les cas 85 et 195 y sont commentes, et 99/100 n'ont pas de .WM donc pas de ligne world.Zones: seule 196 est jouable.
    public static readonly Zone195NokSanSiteCatalog Legacy = new(
    [
        new Zone195NokSanSite(MapId: 196, StoneSlotIndex: 0, LegacyServerNumber: 196)
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
