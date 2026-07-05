namespace Fenrir.Data.Security;

/// <summary>
///     Legacy <c>gmip</c> (<c>MyDB::CheckGMIP</c>). Legacy only consulted this for an elevated account
///     (<c>uUserSort != 0</c>) trying to log in from an unlisted IP; Fenrir's <c>auth.Accounts</c> has no
///     account-grade/GM flag yet (a separate, uncatalogued gap -- see <see cref="ApplicationFirewall" />'s
///     remarks), so that half can't be ported as-is. What *is* ported: an allowlisted IP always bypasses
///     <see cref="IBlockedIpRepository" />/<see cref="IFirewallRuleRepository" />, so an operator's own known-good
///     IP is never caught by a block rule meant for someone else.
/// </summary>
public interface IGmAllowlistRepository
{
    public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct);
}
