using Fenrir.Data.Security;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeGmAllowlistRepository(bool allowed = false) : IGmAllowlistRepository
{
    public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.FromResult(allowed);
    }
}
