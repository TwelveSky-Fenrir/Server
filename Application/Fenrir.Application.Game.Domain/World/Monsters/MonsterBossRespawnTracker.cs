using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed class MonsterBossRespawnTracker(ILogger<MonsterBossRespawnTracker> logger)
{
    private readonly ConcurrentDictionary<int, byte> _dirty = new();
    private readonly ConcurrentDictionary<int, DateTime> _nextSpawnUtc = new();

    private bool _initialized;

        public async Task InitializeAsync(IMonsterBossRespawnTimerRepository repository, CancellationToken ct)
    {
        if (_initialized)
            throw new InvalidOperationException(
                "MonsterBossRespawnTracker.InitializeAsync must only be called once, at boot.");

        var rows = await repository.GetAllAsync(ct).ConfigureAwait(false);
        foreach (var row in rows)
            _nextSpawnUtc[row.MonsterSpawnRegionId] = row.NextSpawnUtc;

        _initialized = true;
    }

        public bool TryGetNextSpawnUtc(int monsterSpawnRegionId, out DateTime nextSpawnUtc)
    {
        return _nextSpawnUtc.TryGetValue(monsterSpawnRegionId, out nextSpawnUtc);
    }

        public void SetNextSpawnUtc(int monsterSpawnRegionId, DateTime nextSpawnUtc)
    {
        _nextSpawnUtc[monsterSpawnRegionId] = nextSpawnUtc;
        _dirty[monsterSpawnRegionId] = 0;
    }

        public async ValueTask FlushDirtyAsync(IMonsterBossRespawnTimerRepository repository, CancellationToken ct)
    {
        if (_dirty.IsEmpty)
            return;

        foreach (var regionId in _dirty.Keys.ToArray())
        {
            if (!_dirty.TryRemove(regionId, out _))
                continue;

            if (!_nextSpawnUtc.TryGetValue(regionId, out var deadline))
                continue;

            try
            {
                await repository.SetAsync(regionId, deadline, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _dirty[regionId] = 0;
                logger.LogError(ex,
                    "MonsterBossRespawnTimer flush failed for region {RegionId} -- will retry next interval",
                    regionId);
            }
        }
    }
}
