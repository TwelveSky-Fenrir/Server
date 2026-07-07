using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Commerce;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     Persists the ShopState=0 durable write for <see cref="Zone" />'s proxy/deputy-shop periodic sweep
///     expiry branch (<c>Zone.RebroadcastProxyShops</c>, Server/ts25zone/S07_MyGame01.cpp:2600-2607) -- the
///     write-behind twin of <see cref="MonsterLootFlushHost" /> for a server-initiated write with no client ack
///     to gate durability on. Keeps every zone's <see cref="Zone.Tick" /> fully synchronous. Also clears the
///     shop's registered display name (game.ProxyShopNames), mirroring the legacy account/DB process's own
///     successful-close side effect (<c>MyDB::CloseProxy</c>, Server/ts25extra/S08_MyDB.cpp:768-795, which
///     removes the name from <c>ProcessForProxyShopName</c>'s in-memory table on the same success path).
/// </summary>
/// <remarks>
///     A dropped/delayed close has no retry (in-memory queue only) -- an accepted residual gap matching
///     <see cref="MonsterLootFlushHost" />'s own posture. This never regresses the in-world behavior either
///     way: the shop is already gone from <see cref="Zone" />'s own broadcast table the instant
///     <c>Zone.RebroadcastProxyShops</c> notices the expiry, regardless of when (or whether) this durable
///     write lands.
///     <para>
///         <b>Process-kill residual risk, exact bound:</b> a true (non-graceful) process kill loses at most
///         <see cref="FlushInterval" /> of queued-but-unpersisted expiry closes -- worst case, a shop the zone
///         already stopped broadcasting reappears as still-open (<c>ShopState</c> never reset to 0) on the
///         next boot until the character reopens/closes it themselves. <see cref="FlushInterval" /> was
///         tightened from the original 5s to 2s in this pass because <see cref="FlushOnceAsync" />'s per-cycle
///         cost with an empty queue is a cheap in-memory list drain per zone, no DB round trip, and a shop
///         naturally expiring is a rare per-zone event relative to the polling cadence. This is an accepted,
///         bounded tradeoff, not a silent gap.
///     </para>
/// </remarks>
public sealed class ProxyShopExpiryFlushHost(
    ZoneRegistry zones,
    IOfflineShopRepository offlineShops,
    ILogger<ProxyShopExpiryFlushHost> logger) : BackgroundService
{
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await FlushOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>Public, not private: exercised directly by tests instead of waiting on the real timer.</summary>
    public async Task FlushOnceAsync(CancellationToken ct)
    {
        foreach (var zone in zones.Zones)
        {
            var characterIds = zone.DrainPendingProxyShopCloses();
            if (characterIds.Count == 0)
                continue;

            foreach (var characterId in characterIds)
                try
                {
                    await offlineShops.SetStateAsync(characterId, 0, ct).ConfigureAwait(false);

                    // Best-effort and deliberately separate from the state write above: game.ProxyShopNames is
                    // a distinct (memory-optimized) table, and a failure clearing it must not be treated as the
                    // close itself having failed -- the shop is already durably closed at this point.
                    try
                    {
                        await offlineShops.SetProxyShopNameAsync(characterId, string.Empty, ct)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex,
                            "Zone {MapId}: proxy-shop display-name clear failed for character {CharacterId} after a successful expiry close",
                            zone.MapId, characterId);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One failed close (e.g. transient SQL failure) must not stop the rest from being tried.
                    logger.LogError(ex,
                        "Zone {MapId}: failed to persist expiry force-close for proxy shop, character {CharacterId}",
                        zone.MapId, characterId);
                }
        }
    }
}
