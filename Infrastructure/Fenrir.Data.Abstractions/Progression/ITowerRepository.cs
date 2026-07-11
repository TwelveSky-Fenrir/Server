using System.Collections.ObjectModel;

namespace Fenrir.Data.Abstractions.Progression;

public interface ITowerRepository
{
    public ValueTask EnsureInitializedAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TowerStateRowDto>> GetAllAsync(CancellationToken ct);

        public ValueTask SetProgressAsync(byte towerIndex, byte level, byte towerType, byte? controllingTribeId,
        CancellationToken ct);
}
