using Fenrir.Application.Game.Domain.Simulation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Simulation;

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
                    timer.Tick(DateTime.Now);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Popup event schedule tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
