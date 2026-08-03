using System.Diagnostics;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class ZoneTickHost(ZoneRegistry zones, ILogger<ZoneTickHost> logger) : BackgroundService
{
    private const int ConsecutiveFailuresBeforePermanentlyDownSignal = 5;
    private static readonly TimeSpan InitialRestartBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRestartBackoff = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan FailureStreakResetThreshold = MaxRestartBackoff;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(zones.Zones.Select(zone => SuperviseZoneAsync(zone, stoppingToken)));
    }

    private async Task SuperviseZoneAsync(Zone zone, CancellationToken stoppingToken)
    {
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = Stopwatch.GetTimestamp();

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
                if (Stopwatch.GetElapsedTime(startedAt) >= FailureStreakResetThreshold)
                    consecutiveFailures = 0;

                consecutiveFailures++;

                logger.LogCritical(ex,
                    "Zone {MapId} tick loop exited unexpectedly and is being restarted; every other zone's " +
                    "ticking is unaffected (consecutive failure {ConsecutiveFailures})", zone.MapId,
                    consecutiveFailures);

                if (consecutiveFailures >= ConsecutiveFailuresBeforePermanentlyDownSignal &&
                    consecutiveFailures % ConsecutiveFailuresBeforePermanentlyDownSignal == 0)
                    logger.LogCritical(
                        "Zone {MapId} has failed {ConsecutiveFailures} consecutive tick-loop restarts and is " +
                        "likely permanently broken (corrupted state or a startup bug), not merely slow to " +
                        "recover; it is still being retried under supervision, but this warrants operator " +
                        "attention distinct from a routine restart", zone.MapId, consecutiveFailures);
            }

            try
            {
                await Task.Delay(ComputeBackoff(consecutiveFailures), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static TimeSpan ComputeBackoff(int consecutiveFailures)
    {
        var scaled = InitialRestartBackoff * Math.Pow(2, consecutiveFailures - 1);
        return scaled < MaxRestartBackoff ? scaled : MaxRestartBackoff;
    }
}
