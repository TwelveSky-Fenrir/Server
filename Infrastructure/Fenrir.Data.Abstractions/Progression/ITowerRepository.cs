using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Progression;

public interface ITowerRepository
{
    public ValueTask EnsureInitializedAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TowerStateRowDto>> GetAllAsync(CancellationToken ct);

    /// <summary>
    ///     Durability write-behind for one tower's progress (game.usp_TowerState_SetController) -- level 0/type 0
    ///     with a null tribe is the post-destruction/never-built resting state.
    /// </summary>
    public ValueTask SetProgressAsync(byte towerIndex, byte level, byte towerType, byte? controllingTribeId,
        CancellationToken ct);
}
