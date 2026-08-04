using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Data.Progression;

public sealed record TowerRepository(ICaeriusNetDbContext Db) : ITowerRepository
{
    public async ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TowerState_EnsureInitialized", 0).Build();
        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ReadOnlyCollection<TowerStateRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TowerState_GetAll", 12).Build();
        return await Db.QueryAsReadOnlyCollectionAsync<TowerStateRowDto>(sp, ct);
    }

    public async ValueTask SetProgressAsync(byte towerIndex, byte level, byte towerType, short attackState,
        byte? controllingTribeId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TowerState_SetController", 0)
            .AddParameter("TowerIndex", towerIndex, SqlDbType.TinyInt)
            .AddParameter("Level", level, SqlDbType.TinyInt)
            .AddParameter("TowerType", towerType, SqlDbType.TinyInt)
            .AddParameter("AttackState", attackState, SqlDbType.SmallInt)
            .AddParameter("ControllingTribeId", (object?)controllingTribeId ?? DBNull.Value, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
