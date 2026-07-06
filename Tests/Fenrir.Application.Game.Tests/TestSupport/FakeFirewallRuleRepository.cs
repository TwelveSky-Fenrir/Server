using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeFirewallRuleRepository(bool blocked = false) : IFirewallRuleRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.FromResult(blocked);
    }

    public ValueTask BlockAsync(string ipAddress, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}
