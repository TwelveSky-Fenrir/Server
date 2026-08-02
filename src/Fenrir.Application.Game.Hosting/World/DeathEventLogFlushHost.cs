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

        // PeriodicTimer.WaitForNextTickAsync permits only one outstanding call at a time; a fresh call
        // issued while a prior one is still pending throws InvalidOperationException. Every racing task
        // below (the timer tick and each zone's wake signal) is therefore created ONCE and held across
        // loop iterations, and only the task that actually won the race is ever replaced -- mirrors
        // Zone.RunAsync (src/Fenrir.Application.Game/Domain/World/Zone.cs) and
        // PositionWriteBehindHost.RetryPendingDisconnectFlushesAsync, which use the same idiom.
        var zoneList = zones.Zones.ToArray();
        var wake = new Task[zoneList.Length];
        for (var i = 0; i < zoneList.Length; i++)
            wake[i] = zoneList[i].WaitForDeathEventLogAsync(stoppingToken);

        var timerTick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        while (!stoppingToken.IsCancellationRequested)
        {
            await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

            Task woken;
            try
            {
                var candidates = new Task[wake.Length + 1];
                Array.Copy(wake, candidates, wake.Length);
                candidates[wake.Length] = timerTick;
                woken = await Task.WhenAny(candidates).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

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
