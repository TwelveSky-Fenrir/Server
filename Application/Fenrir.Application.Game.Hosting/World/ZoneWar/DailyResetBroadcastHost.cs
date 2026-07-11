using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Periodic driver for <see cref="DailyResetBroadcaster" />. Polls well under one minute (15 s) so the
///     one-real-minute-wide 00:01 window can never be missed between polls; the broadcaster itself is
///     idempotent per real minute (<see cref="DailyResetBroadcastScheduler" />), so a shorter or longer poll
///     interval only changes how promptly the 00:01 edge is observed, never correctness.
/// </summary>
public sealed class DailyResetBroadcastHost(
    DailyResetBroadcaster broadcaster,
    ILogger<DailyResetBroadcastHost> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    broadcaster.Tick(DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A missed poll just delays this shard's own 00:01 broadcast to the next cycle -- never
                    // worth crashing the GameServer over.
                    logger.LogError(ex, "Daily-reset broadcast tick failed");
                }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
