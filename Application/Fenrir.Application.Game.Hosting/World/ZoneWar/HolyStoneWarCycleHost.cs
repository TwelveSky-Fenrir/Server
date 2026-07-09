using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="HolyStoneWarCycle" />, armed only on whichever live shard currently
///     hosts <see cref="GameServerOptions.HolyStoneMapId" /> with <see cref="GameServerOptions.HolyStoneWarEnabled" />
///     set -- every other shard's instance of this host is permanently inert. Same shape as
///     <see cref="TribeSymbolBattleSchedulerHost" />/<see cref="TribeVoteElectionCalendarHost" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:793-819 -- legacy gates the equivalent on server number 38;
///     Fenrir shards by map, so "the shard hosting the designated map" is the natural translation.
/// </remarks>
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
                    // A missed tick just delays this cycle's Holy Stone War advance to the next legacy tick --
                    // never worth crashing the GameServer.
                    logger.LogError(ex, "Holy Stone War cycle tick failed");
                }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
