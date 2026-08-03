using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class GameServerDirectoryHeartbeat(
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
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

        var hostedMapIds = zones.Zones.Select(static zone => zone.MapId).ToArray();

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

            try
            {
                await ShardPartitionGuard.EnsureNoOverlapAsync(opts.ShardId, hostedMapIds, directory,
                        shardMapAssignments, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogCritical(ex,
                    "ADR-0012 PARTITION DRIFT for shard {ShardId}: this shard is still actively hosting a map " +
                    "that admin.ShardMapAssignments -- and possibly another live shard -- no longer agrees " +
                    "belongs to it. This is a post-boot re-check, not the one-shot boot guard, and it takes no " +
                    "automatic remediation (stopping a live Zone or migrating its players is out of scope for a " +
                    "heartbeat host). Restart this shard once the correct owner of the conflicting map is " +
                    "confirmed and no shard is left double-hosting it.", opts.ShardId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Post-boot shard-partition re-check failed for shard {ShardId} (transient DB/directory " +
                    "failure, not a detected conflict) -- will retry next heartbeat", opts.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
