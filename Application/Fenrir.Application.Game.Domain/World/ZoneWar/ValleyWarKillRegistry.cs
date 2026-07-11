using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ValleyWarKillRegistry
{
    private readonly ConcurrentDictionary<short, ValleyWarSchedule> _schedules = new();

    public ValleyWarSchedule GetOrCreate(short mapId)
    {
        return _schedules.GetOrAdd(mapId, static _ => new ValleyWarSchedule());
    }

        public void RegisterMonsterKill(short mapId, byte tribeId)
    {
        if (!ValleyWarMapCatalog.Contains(mapId))
            return;

        GetOrCreate(mapId).RegisterMonsterKill(tribeId);
    }
}
