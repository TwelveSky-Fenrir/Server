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
    public short ServerNumber => LegacyServerNumber;

    public bool IsRewardWindowShard => StoneSlotIndex == 0;
}

public sealed class Zone195NokSanSiteCatalog
{
    public static readonly Zone195NokSanSiteCatalog Empty = new([]);

    public static readonly Zone195NokSanSiteCatalog Default = new(
    [
        new Zone195NokSanSite(99, Zone195NokSanState.Server99StoneSlotIndex, 99),
        new Zone195NokSanSite(100, Zone195NokSanState.Server100StoneSlotIndex, 100),
        new Zone195NokSanSite(196, Zone195NokSanState.Server196StoneSlotIndex, 196)
    ]);

    public static readonly Zone195NokSanSiteCatalog Legacy = Default;

        public static bool IsActiveMapId(short mapId) => Zone195NokSanState.IsActiveMapId(mapId);

    private readonly FrozenDictionary<short, Zone195NokSanSite> _byMapId;

    public Zone195NokSanSiteCatalog(IEnumerable<Zone195NokSanSite> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);

        var materializedSites = sites.ToArray();
        if (materializedSites.Any(static site => !Zone195NokSanState.HasExpectedSlot(site.MapId, site.StoneSlotIndex)))
            throw new ArgumentOutOfRangeException(nameof(sites),
                "Nok-San sites are limited to the compiled pairs 196→0, 99→2, and 100→3.");

        if (materializedSites.GroupBy(static site => site.StoneSlotIndex).Any(static group => group.Skip(1).Any()))
            throw new ArgumentException("Each active Nok-San stone slot may be mapped by only one site.", nameof(sites));

        _byMapId = materializedSites.ToFrozenDictionary(static site => site.MapId);
    }

    public bool TryGet(short mapId, out Zone195NokSanSite? site)
    {
        return _byMapId.TryGetValue(mapId, out site);
    }
}
