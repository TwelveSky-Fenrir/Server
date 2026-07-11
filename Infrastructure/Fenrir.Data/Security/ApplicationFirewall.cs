using System.Net;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed class ApplicationFirewall(
    IBlockedIpRepository blockedIps,
    IFirewallRuleRepository firewallRules,
    IGmAllowlistRepository gmAllowlist)
{
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
