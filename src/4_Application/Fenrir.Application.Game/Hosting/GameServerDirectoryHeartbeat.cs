using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class GameServerDirectoryHeartbeat(
    IGameServerDirectoryRepository directory,
    ZoneRegistry zones,
    IOptions<GameServerOptions> options,
    ILogger<GameServerDirectoryHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(opts.HeartbeatIntervalSeconds));

        do
        {
            try
            {
                await directory.HeartbeatAsync(opts.ShardId, opts.PublicHost, opts.Port, zones.TotalPlayerCount,
                        opts.Capacity, 0f, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "GameServerDirectory heartbeat failed for shard {ShardId}", opts.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
