using System.Globalization;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Simulation;

public sealed class PopupEventScheduleHost(
    PopupEventScheduleTimer timer,
    IPopupEventLeaseRepository leases,
    ILogger<PopupEventScheduleHost> logger) : BackgroundService
{
    private const short LeaseDurationSeconds = 30;

    private Guid _leaseOwnerId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var localNow = DateTime.Now;
                try
                {
                    await PublishDueOccurrencesAsync(localNow, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Popup event schedule tick failed");
                }

                var remainder = localNow.Ticks % TimeSpan.TicksPerSecond;
                var delay = TimeSpan.FromTicks(TimeSpan.TicksPerSecond - remainder);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PublishDueOccurrencesAsync(DateTime localNow, CancellationToken ct)
    {
        for (var minutesAgo = 1; minutesAgo >= 0; minutesAgo--)
        {
            var scheduledMinute = localNow.AddMinutes(-minutesAgo);
            foreach (var occurrence in timer.GetDueOccurrences(scheduledMinute))
            {
                var lease = await leases.TryAcquireAsync(CreateOccurrenceKey(occurrence), _leaseOwnerId,
                    LeaseDurationSeconds, ct).ConfigureAwait(false);

                if (!lease.Acquired)
                    continue;

                try
                {
                    timer.Publish(occurrence);
                }
                catch
                {
                    _leaseOwnerId = Guid.NewGuid();
                    throw;
                }
            }
        }
    }

    private static string CreateOccurrenceKey(PopupEventScheduleOccurrence occurrence)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"popup-event:{(byte)occurrence.Type}:{(byte)occurrence.Kind}:{occurrence.ScheduledAtLocal:yyyyMMddHHmm}");
    }
}
