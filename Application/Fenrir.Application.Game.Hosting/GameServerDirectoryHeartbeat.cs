using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>
///     Keeps this shard's row in <c>runtime.GameServerDirectory</c> warm; without it a booted shard is
///     indistinguishable from "no shard available".
/// </summary>
/// <remarks>
///     There is no legacy server-to-server liveness mechanism to port here -- production ts25center/ts25zone
///     links have neither TCP keepalive nor a working reconnect path. <c>SetSocket</c>'s only keepalive branch
///     (<c>Server/Header/socket.h:16-70</c>, the <c>SO_KEEPALIVE</c> block at <c>:44-67</c>) is compiled out:
///     its gating macro <c>USE_KEEPALIVE_SOCKET</c> is commented out at its only definition site
///     (<c>Server/ts25zone/H01_MainApplication.h:10</c>) and is never defined anywhere else in
///     <c>Server/</c>. <c>MyCenterCom::Reconnect()</c> (<c>Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:48-55</c>)
///     -- the only method that would re-establish a dropped center link -- has zero call sites anywhere in
///     <c>Server/</c>; it is declared (<c>Server/ts25zone/H06_MyUpperCom.h:94</c>) and defined but never
///     invoked. The one retry loop that does exist, in <c>MyCenterCom::Init</c>
///     (<c>S06_MyUpperCom02.cpp:12-35</c>), is itself gated on <c>USE_CS_THREAD</c>, which is likewise never
///     defined anywhere in <c>Server/</c> -- so in every shipped build variant a lost center link is simply
///     never recovered without a full process restart. Fenrir's own answer to shard liveness is this
///     DB-mediated heartbeat: any consumer (matchmaking, cross-shard lookups) treats a shard's
///     <c>runtime.GameServerDirectory</c> row as live only within the freshness window checked against
///     <see cref="GameServerOptions.HeartbeatIntervalSeconds" />, and a missed heartbeat here (see the
///     catch below) ages the row out on its own rather than requiring any explicit reconnect logic --
///     already strictly more resilient than the legacy baseline for this entire failure class, with
///     nothing further to port.
/// </remarks>
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
                // TickP99Ms: no tick-duration metric exists yet; 0 is an honest "not measured", and shard-pick is FirstOrDefault, not load-based.
                await directory.HeartbeatAsync(opts.ShardId, opts.PublicHost, opts.Port, zones.TotalPlayerCount,
                        opts.Capacity, 0f, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed heartbeat must not crash the whole GameServer -- the row just ages out of the 15 s freshness window.
                logger.LogError(ex, "GameServerDirectory heartbeat failed for shard {ShardId}", opts.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
