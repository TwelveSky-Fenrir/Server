namespace Fenrir.Data.Abstractions.Security;

/// <summary>
///     Legacy <c>firewall_ip</c> -- see <see cref="FirewallRuleRepository" />'s remarks for what "blocked" means
///     here.
/// </summary>
public interface IFirewallRuleRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct);

    /// <summary>
    ///     Upsert write path for the shared IP-flood-block mechanism
    ///     (<c>Fenrir.Network.Dispatch.FloodProtection.IpFloodGuard</c>) -- also the method any GM-ban/manual
    ///     IP-block tooling should reuse rather than duplicating, since both write the identical row shape
    ///     legacy's <c>SetSqlBlockIP</c> does (<c>Server/Header/enter_count.cpp:59-63</c>): RuleType 3 /
    ///     ANY_BLOCK (<c>Server/ts25firewall/firewall.h:20-27</c>). Upserts (via
    ///     <c>admin.usp_FirewallRule_Upsert</c>) rather than inserting, so a second call against an
    ///     already-blocked IP silently no-ops -- matching legacy's own silent overwrite -- instead of throwing.
    ///     <c>usp_FirewallRule_Add</c>'s insert-only THROW 50303 remains untouched for an explicit GM "add a new
    ///     rule" action, where hitting a duplicate really is an operator mistake worth surfacing loudly.
    /// </summary>
    public ValueTask BlockAsync(string ipAddress, CancellationToken ct);
}
