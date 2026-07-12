using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeBlockedIpRepository(bool blocked = false) : IBlockedIpRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.FromResult(blocked);
    }

    public ValueTask<int> AddAsync(string ipAddress, CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
