using System.Net;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Security;

namespace Fenrir.Data.Tests.Security;

public class ApplicationFirewallTests
{
    private static readonly IPEndPoint SomeEndPoint = new(IPAddress.Parse("203.0.113.7"), 30000);

    [Fact]
    public async Task IsAllowedAsync_NullEndPoint_FailsOpen()
    {
        var firewall = Create(true, true, false);

        Assert.True(await firewall.IsAllowedAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_NothingMatches_Allows()
    {
        var firewall = Create(false, false, false);

        Assert.True(await firewall.IsAllowedAsync(SomeEndPoint, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_BlockedIp_Denies()
    {
        var firewall = Create(true, false, false);

        Assert.False(await firewall.IsAllowedAsync(SomeEndPoint, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_FirewallRuleBlocked_Denies()
    {
        var firewall = Create(false, true, false);

        Assert.False(await firewall.IsAllowedAsync(SomeEndPoint, CancellationToken.None));
    }

    [Fact]
    public async Task IsAllowedAsync_AllowlistedIp_BypassesBothDenyLists()
    {
        var firewall = Create(true, true, true);

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

        public ValueTask BlockAsync(string ipAddress, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeGmAllowlistRepository(bool allowed) : IGmAllowlistRepository
    {
        public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct)
        {
            return ValueTask.FromResult(allowed);
        }

        public ValueTask<int> AddAsync(string ipAddress, CancellationToken ct)
        {
            return ValueTask.FromResult(1);
        }
    }
}
