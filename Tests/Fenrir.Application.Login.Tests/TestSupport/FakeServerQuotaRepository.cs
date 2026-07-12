using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeServerQuotaRepository : IServerQuotaRepository
{
    public int MaxPlayers { get; set; } = 10_000;
    public int GagePlayerNum { get; set; }
    public int CallCount { get; private set; }
    public Exception? Exception { get; set; }

    public ValueTask<ServerQuotaDto> GetAsync(CancellationToken ct)
    {
        CallCount++;
        return Exception is null
            ? ValueTask.FromResult(new ServerQuotaDto(MaxPlayers, GagePlayerNum))
            : throw Exception;
    }
}
