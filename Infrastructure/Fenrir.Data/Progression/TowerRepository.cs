using System.Collections.ObjectModel;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Progression;

/// <summary>
///     game.TowerState access (Server Logic chapter, V9 Progression) -- read-only surface for
///     ZC_BROADCAST_CHUGSOUNG_INFO (152): which tribe controls each of the 12 towers. The tower LEVEL/TYPE
///     progression (CZ_CHUGSOUNG_WAR_UP_SEND, opcode 120) is explicitly OUT OF SCOPE for this pass -- see
///     this feature's own StructuredOutput open issues -- so only <see cref="EnsureInitializedAsync" />/
///     <see cref="GetAllAsync" /> are exposed; there is no SetController caller yet.
/// </summary>
public sealed record TowerRepository(ICaeriusNetDbContext Db) : ITowerRepository
{
    /// <summary>Idempotent bootstrap (usp_TowerState_EnsureInitialized) -- creates the 12 uncontrolled tower rows on the very first call, a no-op afterwards.</summary>
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
