using Fenrir.Data.Security;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeBlockedIpRepository(bool blocked = false) : IBlockedIpRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.FromResult(blocked);
    }
}
