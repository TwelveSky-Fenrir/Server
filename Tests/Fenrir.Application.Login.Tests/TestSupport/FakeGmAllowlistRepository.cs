using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeGmAllowlistRepository(bool allowed = false) : IGmAllowlistRepository
{
    public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.FromResult(allowed);
    }
}
