using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.World;

/// <summary>
///     SQL access surface for the persisted named-boss respawn deadlines (game.MonsterBossRespawnTimers).
///     Fenrir.Application.Game's <c>MonsterBossRespawnTracker</c> is the only intended caller: it owns the
///     in-memory cache and the write-behind cadence, so nothing else should hit this repository directly on a
///     per-tick basis.
/// </summary>
public interface IMonsterBossRespawnTimerRepository
{
    /// <summary>Boot load: every still-recorded deadline, across every zone.</summary>
    public ValueTask<ReadOnlyCollection<MonsterBossRespawnTimerRowDto>> GetAllAsync(CancellationToken ct);

    /// <summary>Idempotent upsert of one region's deadline (game.usp_MonsterBossRespawnTimer_Set).</summary>
    public ValueTask SetAsync(int monsterSpawnRegionId, DateTime nextSpawnUtc, CancellationToken ct);
}
