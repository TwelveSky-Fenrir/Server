using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class DeathEventLogFlushHost(
    ZoneRegistry zones,
    IEventLogRepository eventLog,
    ILogger<DeathEventLogFlushHost> logger) : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

            var wake = zones.Zones.Select(zone => zone.WaitForDeathEventLogAsync(stoppingToken)).ToArray();
            var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

            try
            {
                await Task.WhenAny(timerTick, Task.WhenAny(wake)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task FlushAllZonesAsync(CancellationToken stoppingToken)
    {
        foreach (var zone in zones.Zones)
        {
            var entries = zone.DrainPendingDeathEventLogs();
            if (entries.Count == 0)
                continue;

            foreach (var entry in entries)
                try
                {
                    await eventLog.LogAsync(entry.EventCode, EventLogCategory.Death, null, entry.ActorCharacterId,
                            null, null, entry.ShardId, null, null, null, null, entry.Outcome, entry.Payload,
                            stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex,
                        "Zone {MapId}: failed to persist death EventLog row (EventCode={EventCode}) for character {CharacterId}",
                        zone.MapId, entry.EventCode, entry.ActorCharacterId);
                }
        }
    }
}
