using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class TribeSymbolBattleSchedulerHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zoneRegistry,
    TribeSymbolBattleScheduler scheduler,
    ILogger<TribeSymbolBattleSchedulerHost> logger) : BackgroundService
{
    public bool IsArmed { get; } =
        zoneRegistry.TryGet(options.Value.TribeSymbolBattleMapId, out _) && options.Value.HolyStoneBattleEnabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsArmed)
        {
            logger.LogInformation(
                "TribeSymbolBattleSchedulerHost is inert on this shard (designated map {MapId} not hosted here, or HolyStoneBattleEnabled={Enabled})",
                options.Value.TribeSymbolBattleMapId, options.Value.HolyStoneBattleEnabled);
            return;
        }

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    scheduler.Tick(SimulationClock.LegacyTick, DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Tribe Symbol Battle scheduler tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
