using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     Wall-clock poll driver for <see cref="SessionLivenessSweep" /> (Server/ts25zone/S07_MyGame01.cpp:1963-2006).
///     Same "runs unconditionally on every shard, not gated on any hosted map" posture as
///     <c>TempRegistrationIdleSweepHost</c>: an abandoned raw connection can stall before any map or even any
///     TEMP_REGISTER_SEND admission is resolved for it.
/// </summary>
public sealed class SessionLivenessSweepHost(
    SessionLivenessSweep sweep,
    IOptions<GameServerOptions> options,
    ILogger<SessionLivenessSweepHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SessionLivenessSweepIntervalSeconds));

        do
        {
            try
            {
                sweep.Sweep(DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed sweep just delays an idle disconnect by one cycle -- never worth crashing the GameServer over.
                logger.LogError(ex, "Session liveness sweep failed for shard {ShardId}", options.Value.ShardId);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
