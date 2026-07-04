using System.Collections.ObjectModel;

namespace Fenrir.Data.Progression;

/// <summary>Abstraction over Fenrir.Data.Progression.TowerRepository for DI/testability.</summary>
public interface ITowerRepository
{
    public ValueTask EnsureInitializedAsync(CancellationToken ct);

    public ValueTask<ReadOnlyCollection<TowerStateRowDto>> GetAllAsync(CancellationToken ct);
}
