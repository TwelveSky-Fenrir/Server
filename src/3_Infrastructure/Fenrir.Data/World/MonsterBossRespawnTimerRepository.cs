using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Data.World;

public sealed record MonsterBossRespawnTimerRepository(ICaeriusNetDbContext Db) : IMonsterBossRespawnTimerRepository
{
    public async ValueTask<ReadOnlyCollection<MonsterBossRespawnTimerRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_MonsterBossRespawnTimer_GetAll", 8).Build();

        return await Db.QueryAsReadOnlyCollectionAsync<MonsterBossRespawnTimerRowDto>(sp, ct);
    }

    public async ValueTask SetAsync(int monsterSpawnRegionId, DateTime nextSpawnUtc, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_MonsterBossRespawnTimer_Set", 0)
            .AddParameter("MonsterSpawnRegionId", monsterSpawnRegionId, SqlDbType.Int)
            .AddParameter("NextSpawnUtc", nextSpawnUtc, SqlDbType.DateTime2)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
