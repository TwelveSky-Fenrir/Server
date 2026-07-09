using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="TribeSymbolBattleScheduler" />, armed only on whichever live shard
///     currently hosts <see cref="GameServerOptions.TribeSymbolBattleMapId" /> with
///     <see cref="GameServerOptions.HolyStoneBattleEnabled" /> set -- every other shard's instance of this host
///     is permanently inert. Same "compute <c>IsArmed</c> once at construction, log and return if not armed"
///     shape as <see cref="TribeVoteElectionCalendarHost" />, its sibling scheduler on the same designated
///     legacy content.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:578-622 -- legacy gates the equivalent on server number 37;
///     Fenrir shards by map, so "the shard hosting the designated map" is the natural translation.
/// </remarks>
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
                    // A missed tick just delays this cycle's Tribe Symbol Battle advance to the next legacy
                    // tick -- never worth crashing the GameServer.
                    logger.LogError(ex, "Tribe Symbol Battle scheduler tick failed");
                }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
