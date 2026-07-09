using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeGmAllowlistRepository(bool allowed = false) : IGmAllowlistRepository
{
    public List<string> Added { get; } = [];

    public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.FromResult(allowed);
    }

    public ValueTask<int> AddAsync(string ipAddress, CancellationToken ct)
    {
        Added.Add(ipAddress);
        return ValueTask.FromResult(Added.Count);
    }
}
