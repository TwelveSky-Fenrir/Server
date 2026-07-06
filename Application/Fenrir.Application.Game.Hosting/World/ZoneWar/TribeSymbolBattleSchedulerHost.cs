using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="TribeSymbolBattleScheduler" />, armed only on the one shard configured as
///     server number 37 with <see cref="GameServerOptions.HolyStoneBattleEnabled" /> set -- every other shard's
///     instance of this host is permanently inert. Same "compute <c>_armed</c> once at construction, log and
///     return if not armed" shape as <see cref="TribeVoteElectionCalendarHost" />, its sibling scheduler on the
///     same designated shard.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:578-622 (server-number-37 + enabling-flag gate).
/// </remarks>
public sealed class TribeSymbolBattleSchedulerHost(
    IOptions<GameServerOptions> options,
    TribeSymbolBattleScheduler scheduler,
    ILogger<TribeSymbolBattleSchedulerHost> logger) : BackgroundService
{
    /// <summary>Server/ts25zone/S07_MyGame01.cpp:578-622 -- the one physical instance this scheduler ever arms on.</summary>
    public const int DesignatedShardId = 37;

    public bool IsArmed { get; } = options.Value.ShardId == DesignatedShardId && options.Value.HolyStoneBattleEnabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsArmed)
        {
            logger.LogInformation(
                "TribeSymbolBattleSchedulerHost is inert on this shard (ShardId={ShardId}, HolyStoneBattleEnabled={Enabled})",
                options.Value.ShardId, options.Value.HolyStoneBattleEnabled);
            return;
        }

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                scheduler.Tick(SimulationClock.LegacyTick, DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
