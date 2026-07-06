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
/// </remarks>
public sealed class ProxyShopExpiryFlushHost(
    ZoneRegistry zones,
    IOfflineShopRepository offlineShops,
    ILogger<ProxyShopExpiryFlushHost> logger) : BackgroundService
{
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

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
