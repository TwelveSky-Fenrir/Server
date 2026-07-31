using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ValleyWarKillRegistry
{
    private readonly ConcurrentDictionary<short, ValleyWarSchedule> _schedules = new();

    public ValleyWarSchedule GetOrCreate(short mapId)
    {
        return _schedules.GetOrAdd(mapId, static _ => new ValleyWarSchedule());
    }

    public bool RegisterMonsterKill(short mapId, byte tribeId)
    {
        return ValleyWarMapCatalog.Contains(mapId) && GetOrCreate(mapId).RegisterMonsterKill(tribeId);
    }
}
