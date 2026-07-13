using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Tribes;

public interface ITribeRosterRepository
{
    public ValueTask<ImmutableArray<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct);
}
