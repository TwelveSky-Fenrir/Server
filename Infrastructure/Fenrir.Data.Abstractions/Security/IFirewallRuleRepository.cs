namespace Fenrir.Data.Abstractions.Security;

public interface IFirewallRuleRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct);

        public ValueTask BlockAsync(string ipAddress, CancellationToken ct);

        public ValueTask ReconcileAllowlistAsync(CancellationToken ct);
}
