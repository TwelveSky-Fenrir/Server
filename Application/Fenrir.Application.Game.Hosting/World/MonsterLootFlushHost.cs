using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     Persists server-initiated monster-kill money grants (<c>Zone.QueueMoneyGrant</c>) -- the write-behind
///     twin of <c>PositionWriteBehindHost</c> for kills, which have no client ack to gate durability on.
///     Queuing and flushing on a timer keeps every zone's <see cref="Zone.Tick" /> fully synchronous.
/// </summary>
/// <remarks>
///     The 1s timer is a safety net, not the primary trigger: it's raced via <see cref="Task.WhenAny(Task[])" />
///     against each zone's <see cref="Zone.WaitForMoneyGrantAsync" />, so grants flush almost immediately.
///     A dropped/delayed grant has no retry (in-memory queue only) -- an accepted residual gap.
///     <para>
///         <b>Process-kill residual risk, exact bound:</b> a true (non-graceful) process kill loses whatever
///         money grants are queued in-memory and not yet persisted -- in practice sub-tick, since
///         <see cref="Zone.WaitForMoneyGrantAsync" /> wakes this loop the instant a grant is queued rather than
///         waiting for the timer; the worst-case bound is <see cref="FlushInterval" /> (1s), reached only if
///         the wake signal itself were somehow missed. This timer is left at 1s rather than tightened further
///         in this pass: it is already a safety net racing a near-instant wake, not the primary latency driver,
///         so shortening it further would not meaningfully shrink real-world exposure. This is an accepted,
///         bounded tradeoff, not a silent gap.
///     </para>
/// </remarks>
public sealed class MonsterLootFlushHost(
    ZoneRegistry zones,
    ICharacterRepository characters,
    ILogger<MonsterLootFlushHost> logger) : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await FlushAllZonesAsync(stoppingToken).ConfigureAwait(false);

            var wake = zones.Zones.Select(zone => zone.WaitForMoneyGrantAsync(stoppingToken)).ToArray();
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

    private async Task FlushAllZonesAsync(CancellationToken stoppingToken)
    {
        foreach (var zone in zones.Zones)
        {
            var grants = zone.DrainPendingMoneyGrants();
            if (grants.Count == 0)
                continue;

            foreach (var (characterId, amount) in grants)
                try
                {
                    await characters.AdjustMoneyAsync(characterId, amount, 0, stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One failed grant (e.g. character deleted) must not stop the rest from being tried.
                    logger.LogError(ex,
                        "Zone {MapId}: failed to persist a {Amount}-money kill grant for character {CharacterId}",
                        zone.MapId, amount, characterId);
                }
        }
    }
}
