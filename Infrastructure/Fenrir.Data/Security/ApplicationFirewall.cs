using System.Net;

namespace Fenrir.Data.Security;

/// <summary>
///     Composes the IP-address-keyed half of cluster C02's firewall (account/character bans are a separate,
///     identity-keyed check -- see <see cref="IBanRepository" />). Checked at both Login pre-auth and Zone
///     world-entry (ADR-0012: Login and Zone are separate TCP listeners, so an IP block only enforced at Login
///     wouldn't stop a client that reaches Zone directly with a valid ticket).
/// </summary>
/// <remarks>
///     Legacy also gated this on account grade (<c>uUserSort != 0</c> = GM account must connect from an
///     allowlisted IP, <c>Server/ts25login/S04_MyWork02.cpp:192-201</c>) -- <c>auth.Accounts</c> has no
///     account-grade/GM flag in Fenrir today, so that half isn't ported; only the safe, identity-independent
///     half is: an allowlisted IP always bypasses the deny-lists below.
/// </remarks>
public sealed class ApplicationFirewall(
    IBlockedIpRepository blockedIps,
    IFirewallRuleRepository firewallRules,
    IGmAllowlistRepository gmAllowlist)
{
    /// <summary>Fail-open when no address is known (unit tests, non-TCP transport) -- defense-in-depth, not the sole guard.</summary>
    public async ValueTask<bool> IsAllowedAsync(IPEndPoint? remoteEndPoint, CancellationToken ct)
    {
        if (remoteEndPoint is null)
            return true;

        var ip = remoteEndPoint.Address.ToString();

        if (await gmAllowlist.IsAllowedAsync(ip, ct))
            return true;

        if (await blockedIps.IsBlockedAsync(ip, ct))
            return false;

        return !await firewallRules.IsBlockedAsync(ip, ct);
    }
}
