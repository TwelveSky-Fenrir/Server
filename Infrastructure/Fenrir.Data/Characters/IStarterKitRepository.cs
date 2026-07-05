namespace Fenrir.Data.Characters;

/// <summary>
///     Read-only catalog behind op17's starter kit (equipment/inventory/skills/hotkeys per starting-kit
///     template, plus the resolved spawn map's default landing point). Small and rarely queried (once per
///     character creation), so unlike world.WorldDataCache this is a plain per-request query, not a
///     boot-time cache.
/// </summary>
public interface IStarterKitRepository
{
    public ValueTask<StarterKitBundle> GetByPreviousTribeAsync(byte previousTribe, short mapId, CancellationToken ct);
}
