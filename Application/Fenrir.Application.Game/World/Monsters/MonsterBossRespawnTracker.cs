using System.Collections.Concurrent;
using Fenrir.Data.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     Process-wide, thread-safe cache of persisted "next spawn" deadlines for the handful of named-boss
///     <c>world.MonsterSpawnRegions</c> slots whose respawn cooldown must survive a GameServer restart --
///     the modern replacement for the legacy LNW33 YangGok persistence (<c>S10_MySummon.cpp</c>'s
///     <c>YG_SaveNormalBossNext</c>/<c>YG_LoadNormalBossNext</c>, one flat file per zone/slot/bossId under
///     <c>YanggokNormalBossTimer\</c>), normalized here into <c>game.MonsterBossRespawnTimers</c>.
/// </summary>
/// <remarks>
///     Boot-loads once via <see cref="InitializeAsync" /> -- before any zone starts ticking, so
///     <see cref="MonsterSpawnScheduler.BuildState" /> can consult <see cref="TryGetNextSpawnUtc" /> from the
///     tick thread as a pure in-memory read, never touching SQL synchronously (SQL is a durability journal,
///     never read again once loaded). Mutated only by <see cref="MonsterSpawnScheduler" />'s own tick
///     (<see cref="SetNextSpawnUtc" />) and flushed back by <see cref="MonsterBossRespawnWriteBehindHost" />.
///     <para>
///         A region's row is never deleted once written, even after the boss successfully respawns -- the next
///         death simply overwrites it with a fresh deadline, and a stale (past) deadline is harmless: boot-load
///         treats it as "already due", identical to no row at all. A handful of permanent rows is an acceptable
///         trade for not needing a delete round trip on every single respawn.
///     </para>
/// </remarks>
public sealed class MonsterBossRespawnTracker(ILogger<MonsterBossRespawnTracker> logger)
{
    private readonly ConcurrentDictionary<int, byte> _dirty = new();
    private readonly ConcurrentDictionary<int, DateTime> _nextSpawnUtc = new();

    private bool _initialized;

    /// <summary>Idempotent-bootstrap-free load: game.MonsterBossRespawnTimers has nothing to seed, only to read.</summary>
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

    /// <summary>Tick-thread-safe: a plain in-memory lookup, never SQL.</summary>
    public bool TryGetNextSpawnUtc(int monsterSpawnRegionId, out DateTime nextSpawnUtc)
    {
        return _nextSpawnUtc.TryGetValue(monsterSpawnRegionId, out nextSpawnUtc);
    }

    /// <summary>Tick-owned caller only (<see cref="MonsterSpawnScheduler" />, on arming a boss slot's respawn timer).</summary>
    public void SetNextSpawnUtc(int monsterSpawnRegionId, DateTime nextSpawnUtc)
    {
        _nextSpawnUtc[monsterSpawnRegionId] = nextSpawnUtc;
        _dirty[monsterSpawnRegionId] = 0;
    }

    /// <summary>
    ///     Persists every region touched by <see cref="SetNextSpawnUtc" /> since the last flush; no-ops
    ///     otherwise. Never throws: a failed write is logged and left dirty for the next interval to retry.
    /// </summary>
    public async ValueTask FlushDirtyAsync(IMonsterBossRespawnTimerRepository repository, CancellationToken ct)
    {
        if (_dirty.IsEmpty)
            return;

        foreach (var regionId in _dirty.Keys.ToArray())
        {
            if (!_dirty.TryRemove(regionId, out _))
                continue; // another flush already claimed it

            if (!_nextSpawnUtc.TryGetValue(regionId, out var deadline))
                continue; // defensive only -- SetNextSpawnUtc always writes both together

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
