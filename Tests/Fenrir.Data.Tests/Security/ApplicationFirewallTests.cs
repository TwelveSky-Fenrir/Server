using System.Net;
using Fenrir.Data.Security;

namespace Fenrir.Data.Tests.Security;

public class ApplicationFirewallTests
{
    private static readonly IPEndPoint SomeEndPoint = new(IPAddress.Parse("203.0.113.7"), 30000);

    [Fact]
    public async Task IsAllowedAsync_NullEndPoint_FailsOpen()
    {
        var firewall = Create(blocked: true, ruleBlocked: true, allowlisted: false);

        Assert.True(await firewall.IsAllowedAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_NothingMatches_Allows()
    {
        var firewall = Create(blocked: false, ruleBlocked: false, allowlisted: false);

        Assert.True(await firewall.IsAllowedAsync(SomeEndPoint, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_BlockedIp_Denies()
    {
        var firewall = Create(blocked: true, ruleBlocked: false, allowlisted: false);

        Assert.False(await firewall.IsAllowedAsync(SomeEndPoint, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_FirewallRuleBlocked_Denies()
    {
        var firewall = Create(blocked: false, ruleBlocked: true, allowlisted: false);

        Assert.False(await firewall.IsAllowedAsync(SomeEndPoint, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_AllowlistedIp_BypassesBothDenyLists()
    {
        var firewall = Create(blocked: true, ruleBlocked: true, allowlisted: true);

        Assert.True(await firewall.IsAllowedAsync(SomeEndPoint, CancellationToken.None));
    }

    private static ApplicationFirewall Create(bool blocked, bool ruleBlocked, bool allowlisted)
    {
        return new ApplicationFirewall(
            new FakeBlockedIpRepository(blocked),
            new FakeFirewallRuleRepository(ruleBlocked),
            new FakeGmAllowlistRepository(allowlisted));
    }

    private sealed class FakeBlockedIpRepository(bool blocked) : IBlockedIpRepository
    {
        public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
        {
            return ValueTask.FromResult(blocked);
        }
    }

    private sealed class FakeFirewallRuleRepository(bool blocked) : IFirewallRuleRepository
    {
        public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
        {
            return ValueTask.FromResult(blocked);
        }
    }

    private sealed class FakeGmAllowlistRepository(bool allowed) : IGmAllowlistRepository
    {
        public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct)
        {
            return ValueTask.FromResult(allowed);
        }
    }
}
