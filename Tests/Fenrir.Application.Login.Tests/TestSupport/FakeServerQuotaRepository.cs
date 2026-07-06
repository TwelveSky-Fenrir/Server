using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IServerQuotaRepository: defaults to a comfortably large cap (never maintenance, never
// full) so tests that don't care about the capacity gates aren't accidentally tripped by it.
internal sealed class FakeServerQuotaRepository : IServerQuotaRepository
{
    public int MaxPlayers { get; set; } = 10_000;
    public int CallCount { get; private set; }
    public Exception? Exception { get; set; }

    public ValueTask<int> GetMaxPlayersAsync(CancellationToken ct)
    {
        CallCount++;
        return Exception is null ? ValueTask.FromResult(MaxPlayers) : throw Exception;
    }
}
