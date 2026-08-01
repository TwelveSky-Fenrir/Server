using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class HolyStoneTerritoryEvictionSweepHost(
    HolyStoneTerritoryEvictionSweep sweep,
    ILogger<HolyStoneTerritoryEvictionSweepHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    sweep.Tick(1);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Holy Stone territory eviction sweep tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
