using Fenrir.Security.FloodProtection;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Login.Hosting;

public sealed class FirewallAllowlistReconcileHost(FirewallAllowlistReconcileService service) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return service.RunAsync(stoppingToken);
    }
}
