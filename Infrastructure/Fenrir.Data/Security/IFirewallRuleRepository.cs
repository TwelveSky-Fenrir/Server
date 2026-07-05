namespace Fenrir.Data.Security;

/// <summary>Legacy <c>firewall_ip</c> -- see <see cref="FirewallRuleRepository" />'s remarks for what "blocked" means here.</summary>
public interface IFirewallRuleRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct);
}
