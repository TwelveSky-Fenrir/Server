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

        var zoneList = zones.Zones.ToArray();
        var wake = new Task[zoneList.Length];
        for (var i = 0; i < zoneList.Length; i++)
            wake[i] = zoneList[i].WaitForDeathEventLogAsync(stoppingToken);

        var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

                var candidates = new Task[wake.Length + 1];
                Array.Copy(wake, candidates, wake.Length);
                candidates[wake.Length] = timerTick;
                var woken = await Task.WhenAny(candidates).ConfigureAwait(false);

                if (ReferenceEquals(woken, timerTick))
                {
                    timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
                    continue;
                }

                for (var i = 0; i < wake.Length; i++)
                {
                    if (!ReferenceEquals(wake[i], woken))
                        continue;

                    wake[i] = zoneList[i].WaitForDeathEventLogAsync(stoppingToken);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        await FlushAllZonesAsync(CancellationToken.None).ConfigureAwait(false);
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
