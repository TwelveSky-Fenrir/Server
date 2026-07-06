using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="HolyStoneTerritoryEvictionSweep" /> -- unlike its two sibling hosts, no
///     dedicated <c>ShardId</c> gate is needed: Fenrir shards by map (same translation
///     <see cref="Domain.GameServerOptions.Zone241DungeonMapIds" /> already uses), so
///     <see cref="Domain.GameServerOptions.HolyStoneTerritoryMapIds" /> being empty on a shard that hosts none
///     of the Holy Stone's territory maps already makes every sweep a no-op on that shard.
/// </summary>
public sealed class HolyStoneTerritoryEvictionSweepHost(HolyStoneTerritoryEvictionSweep sweep) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                sweep.Tick(SimulationClock.LegacyTick);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
