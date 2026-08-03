using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.WorldState;

public sealed class TribeBankTaxSweepFlushHost(
    ZoneRegistry zones,
    ITribeBankTaxSweepGateway gateway,
    ILogger<TribeBankTaxSweepFlushHost> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public async Task FlushOnceAsync(CancellationToken ct)
    {
        foreach (var zone in zones.Zones)
        {
            var sweeps = zone.DrainPendingTribeBankTaxSweeps();
            foreach (var sweep in sweeps)
                try
                {
                    await gateway.SweepAsync(zone.MapId, sweep, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Zone {MapId}: tribe bank tax sweep send failed -- window's tax lost",
                        zone.MapId);
                }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            do
            {
                await FlushOnceAsync(stoppingToken).ConfigureAwait(false);
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }

        await FlushOnceAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
