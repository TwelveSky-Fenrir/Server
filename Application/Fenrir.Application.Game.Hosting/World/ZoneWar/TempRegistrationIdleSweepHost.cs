using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

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
                logger.LogError(ex, "TEMP_REGISTER_SEND idle sweep failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
