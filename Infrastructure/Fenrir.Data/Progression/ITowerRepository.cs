using System.Collections.ObjectModel;

namespace Fenrir.Data.Progression;

public interface ITowerRepository
{
    public ValueTask EnsureInitializedAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TowerStateRowDto>> GetAllAsync(CancellationToken ct);
}
