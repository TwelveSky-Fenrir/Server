using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class RegularWarActiveMapTracker
{
    private readonly ConcurrentDictionary<short, RegularWarPhase> _phaseByMapId = new();

    public void ReportPhase(short mapId, RegularWarPhase phase)
    {
        _phaseByMapId[mapId] = phase;
    }

    public bool IsBattleInProgress(short mapId)
    {
        return RegularWarMapCatalog.TryGet(mapId, out _) &&
               _phaseByMapId.TryGetValue(mapId, out var phase) &&
               phase == RegularWarPhase.Active;
    }

    public bool IsFightClosed(short mapId)
    {
        return RegularWarMapCatalog.TryGet(mapId, out _) &&
               _phaseByMapId.TryGetValue(mapId, out var phase) &&
               phase >= RegularWarPhase.PostWarCleanup;
    }
}
