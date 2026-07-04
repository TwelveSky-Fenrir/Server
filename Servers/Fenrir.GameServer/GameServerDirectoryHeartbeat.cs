using Fenrir.Application.Game;
using Fenrir.Application.Game.World;
using Fenrir.Data.Runtime;
using Microsoft.Extensions.Options;

namespace Fenrir.GameServer;

/// <summary>
///     Keeps this shard's row in <c>runtime.GameServerDirectory</c> warm so LoginServer's directory query
///     (§11.4, 2 s cache) finds a live destination. Without this, a shard that boots but never heartbeats is
///     invisible to every CL_DEMAND_ZONE_SERVER_INFO_SEND, indistinguishable from "no shard available".
/// </summary>
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
                // TickP99Ms: no tick-duration metric pipeline exists yet in M1 (OpenTelemetry wiring is a Phase 8
                // concern, architecture reference §13) -- 0 is an honest "not measured yet", not a fabricated
                // number, and it never affects GetDirectoryAsync's selection (LoginServer's M1 shard-pick is
                // FirstOrDefault, not load-based -- see ZoneTransferHandler's own comment).
                await directory.HeartbeatAsync(opts.ShardId, opts.PublicHost, opts.Port, zones.TotalPlayerCount,
                        opts.Capacity, 0f, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed heartbeat must not crash the whole GameServer -- the row simply ages out of
                // usp_GameServer_GetDirectory's 15 s freshness window and this shard stops being offered until
                // the next successful beat.
                logger.LogError(ex, "GameServerDirectory heartbeat failed for shard {ShardId}", opts.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
