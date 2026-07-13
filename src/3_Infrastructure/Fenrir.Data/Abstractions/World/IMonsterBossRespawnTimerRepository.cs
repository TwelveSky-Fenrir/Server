using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.World;

public interface IMonsterBossRespawnTimerRepository
{
    public ValueTask<ReadOnlyCollection<MonsterBossRespawnTimerRowDto>> GetAllAsync(CancellationToken ct);

    public ValueTask SetAsync(int monsterSpawnRegionId, DateTime nextSpawnUtc, CancellationToken ct);
}
