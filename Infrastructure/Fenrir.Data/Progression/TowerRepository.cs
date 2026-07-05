using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Data.Progression;

// Durability journal for Application.Fenrir.Game.Progression.TowerWarState: which tribe controls each of the 12
// towers, and their Level/TowerType (the legacy mTowerInfo->mState1Tower[i] broadcast value) so a GameServer
// restart resumes the guardian-monster lifecycle instead of losing it.
public sealed record TowerRepository(ICaeriusNetDbContext Db) : ITowerRepository
{
    /// <summary>Idempotent bootstrap: creates the 12 uncontrolled tower rows on first call, no-op afterwards.</summary>
    public async ValueTask EnsureInitializedAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TowerState_EnsureInitialized", 0).Build();
        await Db.ExecuteAsync(sp, ct);
    }

    /// <summary>All 12 towers, ordered by TowerIndex (usp_TowerState_GetAll).</summary>
    public async ValueTask<ReadOnlyCollection<TowerStateRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TowerState_GetAll", 12).Build();
        return await Db.QueryAsReadOnlyCollectionAsync<TowerStateRowDto>(sp, ct);
    }

    public async ValueTask SetProgressAsync(byte towerIndex, byte level, byte towerType, byte? controllingTribeId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_TowerState_SetController", 0)
            .AddParameter("TowerIndex", towerIndex, SqlDbType.TinyInt)
            .AddParameter("Level", level, SqlDbType.TinyInt)
            .AddParameter("TowerType", towerType, SqlDbType.TinyInt)
            .AddParameter("ControllingTribeId", (object?)controllingTribeId ?? DBNull.Value, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
