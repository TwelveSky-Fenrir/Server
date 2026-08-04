using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class RegularWarActiveMapTracker
{
    public const int PendingKillQueueCap = 8192;

    private readonly ConcurrentDictionary<short, ConcurrentQueue<PendingRegularWarKill>> _pendingKillsByMapId = new();

    private readonly ConcurrentDictionary<short, RegularWarPhase> _phaseByMapId = new();

    private readonly ConcurrentDictionary<short, int> _warCycleByMapId = new();

    public void ReportPhase(short mapId, RegularWarPhase phase)
    {
        _phaseByMapId[mapId] = phase;
    }

    public void ReportWarCycle(short mapId, int warCycleNumber)
    {
        _warCycleByMapId[mapId] = warCycleNumber;
    }

    public int GetWarCycle(short mapId)
    {
        return _warCycleByMapId.GetValueOrDefault(mapId);
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

    public void RegisterKill(short mapId, byte killerTribe, int killerCharacterId)
    {
        if (!IsBattleInProgress(mapId))
            return;

        var queue = _pendingKillsByMapId.GetOrAdd(mapId, static _ => new ConcurrentQueue<PendingRegularWarKill>());
        if (queue.Count >= PendingKillQueueCap)
            return;

        queue.Enqueue(new PendingRegularWarKill(killerTribe, killerCharacterId));
    }

    public void DrainPendingKills(short mapId, RegularWarSchedule schedule)
    {
        if (!_pendingKillsByMapId.TryGetValue(mapId, out var queue))
            return;

        while (queue.TryDequeue(out var kill))
            schedule.RegisterKill(kill.KillerTribe, kill.KillerCharacterId);
    }

    private readonly record struct PendingRegularWarKill(byte KillerTribe, int KillerCharacterId);
}
