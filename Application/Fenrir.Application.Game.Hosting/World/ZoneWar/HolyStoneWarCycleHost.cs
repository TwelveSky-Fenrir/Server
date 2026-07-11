using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class HolyStoneWarCycleHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zoneRegistry,
    HolyStoneWarCycle cycle,
    ILogger<HolyStoneWarCycleHost> logger) : BackgroundService
{
    public bool IsArmed { get; } =
        zoneRegistry.TryGet(options.Value.HolyStoneMapId, out _) && options.Value.HolyStoneWarEnabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsArmed)
        {
            logger.LogInformation(
                "HolyStoneWarCycleHost is inert on this shard (designated map {MapId} not hosted here, or HolyStoneWarEnabled={Enabled})",
                options.Value.HolyStoneMapId, options.Value.HolyStoneWarEnabled);
            return;
        }

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    cycle.Tick(SimulationClock.LegacyTick);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Holy Stone War cycle tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
