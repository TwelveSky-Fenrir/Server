using System.Collections.ObjectModel;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Progression;

// Read-only surface for ZC_BROADCAST_CHUGSOUNG_INFO (152): which tribe controls each of 12 towers. Tower level/type progression (CZ_CHUGSOUNG_WAR_UP_SEND) is out of scope -- no SetController yet.
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
}
