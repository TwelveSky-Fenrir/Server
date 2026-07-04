using Fenrir.Data.Characters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.World;

/// <summary>
///     Persists server-initiated monster-kill money grants (<c>Zone.QueueMoneyGrant</c>, report
///     ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §5's <c>DROP_MONEY</c>) -- the write-behind twin
///     of <c>PositionWriteBehindHost</c> for exactly one narrow case: a kill reward has no client
///     request/ack to gate durability on (unlike a client-initiated pickup, which
///     <c>GenericActionHandler</c> awaits synchronously per the D7 regime), so queuing it and flushing on a
///     short timer keeps every zone's <see cref="Zone.Tick" /> fully synchronous (never blocking on SQL I/O)
///     without ever silently dropping a reward.
/// </summary>
/// <remarks>
///     A short (1 s) flush interval is kept as a SAFETY-NET cadence, not the primary trigger: every zone's
///     <see cref="Zone.WaitForMoneyGrantAsync" /> is raced against the timer via <see cref="Task.WhenAny(Task[])" />,
///     so in practice a grant is flushed within roughly one SQL round trip of being queued, not up to a full
///     second late (review finding: the fixed-interval-only design left a wider, avoidable in-memory-only
///     window than the rest of the D7-regime economy code accepts). The timer still fires on its own if no
///     grant arrives, and remains the only trigger a test/idle zone with zero kills ever needs. Unlike a
///     position (which self-heals on the very next accepted move), a dropped/delayed money grant is a one-shot
///     reward with no natural retry -- an unavoidable residual gap (the queue is in-memory only, exactly like
///     <see cref="Zone" />'s own ground-item pool) is still accepted, just made as small as this architecture
///     (the zone tick must never block on SQL I/O) allows without giving the tick thread its own SQL call.
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
                    // One failed grant (e.g. the character was deleted between the kill and this flush)
                    // must never stop every OTHER queued grant, in this zone or any other, from being tried.
                    logger.LogError(ex,
                        "Zone {MapId}: failed to persist a {Amount}-money kill grant for character {CharacterId}",
                        zone.MapId, amount, characterId);
                }
        }
    }
}
