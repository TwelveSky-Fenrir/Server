using Fenrir.Network.Dispatch.FloodProtection;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Login.Hosting;

/// <summary>Wall-clock driver for <see cref="FirewallAllowlistReconcileService" /> (legacy RemoveIPTick, ~120s).</summary>
public sealed class FirewallAllowlistReconcileHost(FirewallAllowlistReconcileService service) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return service.RunAsync(stoppingToken);
    }
}
