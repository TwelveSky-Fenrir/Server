using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Wall-clock poll driver for <see cref="TempRegistrationIdleSweep" /> (Server/ts25zone/S07_MyGame01.cpp:1963-1990).
///     Unlike the per-tick ZoneWar schedulers, this isn't gated on any hosted map/ShardId -- every shard runs
///     it unconditionally, since a TEMP_REGISTER_SEND (op11) connection can stall before any map is even
///     resolved for it.
/// </summary>
public sealed class TempRegistrationIdleSweepHost(
    TempRegistrationIdleSweep sweep,
    IOptions<GameServerOptions> options,
    ILogger<TempRegistrationIdleSweepHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(options.Value.TempRegistrationIdleSweepIntervalSeconds));

        do
        {
            try
            {
                sweep.Sweep(DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed sweep just delays an idle disconnect by one cycle -- never worth crashing the GameServer over.
                logger.LogError(ex, "TEMP_REGISTER_SEND idle sweep failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
