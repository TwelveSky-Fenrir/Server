using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     Persists death-related <c>game.EventLog</c> rows queued by <see cref="Zone.ApplyDeath" /> /
///     <see cref="Zone.ApplyDeathExperienceLoss" /> (<c>Zone.QueueDeathEventLog</c>) -- the write-behind twin
///     of <see cref="MonsterLootFlushHost" /> for a domain event with no client ack to gate durability on.
///     Keeps every zone's <see cref="Zone.Tick" /> fully synchronous: the actual audit write always goes
///     through <see cref="IEventLogRepository.LogAsync" />, once per queued row -- the single-row, high-stakes
///     path that interface's own remarks explicitly enumerate deaths under, never the high-frequency
///     <c>BatchLogAsync</c>/<c>EventLogQueue</c> path reserved for low-stakes telemetry.
/// </summary>
/// <remarks>
///     A dropped/delayed row (e.g. sustained SQL outage) has no retry (in-memory queue only) -- an accepted
///     residual gap matching <see cref="MonsterLootFlushHost" />'s own posture. This never regresses the
///     in-world outcome either way: the death (and any XP/CP loss it caused) is already fully applied on
///     <see cref="PlayerRuntimeState" /> and durable via the normal progression write-behind path regardless
///     of whether this audit row lands.
///     <para>
///         <b>Process-kill residual risk, exact bound:</b> a true (non-graceful) process kill loses whatever
///         death-audit rows are queued in-memory and not yet persisted -- in practice sub-tick, since
///         <see cref="Zone.WaitForDeathEventLogAsync" /> wakes this loop the instant a row is queued rather
///         than waiting for the timer; the worst-case bound is <see cref="FlushInterval" /> (1s), same
///         "safety net, not the primary trigger" posture as <see cref="MonsterLootFlushHost" />, and left
///         unshortened for the same reason. As noted above, this loss is audit-log-only -- it never affects
///         the already-durable in-game outcome of the death itself. This is an accepted, bounded tradeoff, not
///         a silent gap.
///     </para>
/// </remarks>
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

    /// <summary>Public, not private: exercised directly by tests instead of waiting on the real timer.</summary>
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
                    // One failed audit write (e.g. transient SQL failure) must not stop the rest from being
                    // tried, and must never affect the already-applied, already-durable death/XP-loss itself.
                    logger.LogError(ex,
                        "Zone {MapId}: failed to persist death EventLog row (EventCode={EventCode}) for character {CharacterId}",
                        zone.MapId, entry.EventCode, entry.ActorCharacterId);
                }
        }
    }
}
