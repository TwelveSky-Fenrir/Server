using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class ZoneTickHost(ZoneRegistry zones, ILogger<ZoneTickHost> logger) : BackgroundService
{
    private static readonly TimeSpan RestartBackoff = TimeSpan.FromSeconds(1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(zones.Zones.Select(zone => SuperviseZoneAsync(zone, stoppingToken)));
    }

    private async Task SuperviseZoneAsync(Zone zone, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await zone.RunAsync(stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Zone {MapId} tick loop exited unexpectedly and is being restarted; every other zone's " +
                    "ticking is unaffected", zone.MapId);
            }

            try
            {
                await Task.Delay(RestartBackoff, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
