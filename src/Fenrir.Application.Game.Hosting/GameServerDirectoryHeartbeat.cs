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

        logger.LogInformation(
            "GameServerDirectory heartbeat host started for shard {ShardId}; first registration attempt to " +
            "runtime.GameServerDirectory as {Host}:{Port} follows (every {IntervalSeconds}s)", opts.ShardId,
            opts.PublicHost, opts.Port, opts.HeartbeatIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(opts.HeartbeatIntervalSeconds));

        var registered = false;
        do
        {
            try
            {
                await directory.HeartbeatAsync(opts.ShardId, opts.PublicHost, opts.Port, zones.TotalPlayerCount,
                        opts.Capacity, 0f, stoppingToken)
                    .ConfigureAwait(false);

                if (!registered)
                {
                    logger.LogInformation(
                        "GameServer registered in runtime.GameServerDirectory as shard {ShardId} at {Host}:{Port} " +
                        "(capacity {Capacity}) -- this is the address the LoginServer offers clients and probes",
                        opts.ShardId, opts.PublicHost, opts.Port, opts.Capacity);
                    registered = true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "GameServerDirectory heartbeat failed for shard {ShardId}", opts.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
