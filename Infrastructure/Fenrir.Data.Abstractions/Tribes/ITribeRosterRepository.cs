using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Tribes;

public interface ITribeRosterRepository
{
    // Polled every ~6 ticks by TribePointRecomputeHost -- ImmutableArray is the polled/hot-path terminal
    // call shape, not ReadOnlyCollection (request/response shape).
    public ValueTask<ImmutableArray<TribeRosterCharacterDto>> GetForTribePointAsync(CancellationToken ct);
}
