using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Commerce;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

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
        }
    }

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
                    logger.LogError(ex,
                        "Zone {MapId}: failed to persist expiry force-close for proxy shop, character {CharacterId}",
                        zone.MapId, characterId);
                }
        }
    }
}
