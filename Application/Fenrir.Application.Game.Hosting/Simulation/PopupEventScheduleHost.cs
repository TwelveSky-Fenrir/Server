using Fenrir.Application.Game.Domain.Simulation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Simulation;

/// <summary>
///     Periodic driver for <see cref="PopupEventScheduleTimer" />. Polls every 5 s -- comfortably under a
///     minute, so no minute-granular transition (countdown/open/close) can ever be missed between polls; the
///     timer itself is edge-triggered per real minute, so a shorter or longer poll interval only changes how
///     promptly a transition is observed, never correctness or duplicate firing.
/// </summary>
public sealed class PopupEventScheduleHost(
    PopupEventScheduleTimer timer,
    ILogger<PopupEventScheduleHost> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var poll = new PeriodicTimer(PollInterval);

        try
        {
            while (await poll.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    timer.Tick(DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A missed poll just delays this cycle's popup-window transition to the next poll -- never
                    // worth crashing the GameServer over.
                    logger.LogError(ex, "Popup event schedule tick failed");
                }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
