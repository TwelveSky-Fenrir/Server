using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Admin;

public interface IMuteRepository
{

        public ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct);

        public ValueTask<ImmutableArray<int>> GetActiveCharacterIdsAsync(IReadOnlyCollection<int> characterIds,
        CancellationToken ct);
}
