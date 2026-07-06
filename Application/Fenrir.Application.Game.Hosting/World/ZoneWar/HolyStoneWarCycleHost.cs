using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="HolyStoneWarCycle" />, armed only on the one shard configured as server
///     number 38 with <see cref="GameServerOptions.HolyStoneWarEnabled" /> set -- every other shard's instance
///     of this host is permanently inert. Same shape as <see cref="TribeSymbolBattleSchedulerHost" />/
///     <see cref="TribeVoteElectionCalendarHost" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:793-819 (server-number-38 + enabling-flag gate).
/// </remarks>
public sealed class HolyStoneWarCycleHost(
    IOptions<GameServerOptions> options,
    HolyStoneWarCycle cycle,
    ILogger<HolyStoneWarCycleHost> logger) : BackgroundService
{
    /// <summary>Server/ts25zone/S07_MyGame01.cpp:793-819 -- the one physical instance this scheduler ever arms on.</summary>
    public const int DesignatedShardId = 38;

    private readonly bool _armed = options.Value.ShardId == DesignatedShardId && options.Value.HolyStoneWarEnabled;

    public bool IsArmed => _armed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_armed)
        {
            logger.LogInformation(
                "HolyStoneWarCycleHost is inert on this shard (ShardId={ShardId}, HolyStoneWarEnabled={Enabled})",
                options.Value.ShardId, options.Value.HolyStoneWarEnabled);
            return;
        }

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                cycle.Tick(SimulationClock.LegacyTick);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
