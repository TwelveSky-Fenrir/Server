using Fenrir.Data.Security;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeFirewallRuleRepository(bool blocked = false) : IFirewallRuleRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.FromResult(blocked);
    }
}
