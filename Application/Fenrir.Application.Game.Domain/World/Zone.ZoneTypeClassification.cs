namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    public bool IsZone126TypeZone => options.Zone126TypeMapIds.Contains(MapId);

    public bool IsZone039TypeZone => options.Zone039TypeMapIds.Contains(MapId);

    public bool IsDungeonServerZone => DungeonServerZoneCatalog.IsDungeonServer(MapId);
}
