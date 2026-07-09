using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="HolyStoneTerritoryEvictionSweep" /> -- unlike its two sibling hosts, no
///     dedicated <c>ShardId</c> gate is needed: Fenrir shards by map (same translation
///     <see cref="Domain.GameServerOptions.Zone241DungeonMapIds" /> already uses), so
///     <see cref="Domain.GameServerOptions.HolyStoneTerritoryMapIds" /> being empty on a shard that hosts none
///     of the Holy Stone's territory maps already makes every sweep a no-op on that shard.
/// </summary>
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
                    sweep.Tick(SimulationClock.LegacyTick);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A missed tick just delays this cycle's Holy Stone territory eviction sweep to the next
                    // legacy tick -- never worth crashing the GameServer.
                    logger.LogError(ex, "Holy Stone territory eviction sweep tick failed");
                }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
