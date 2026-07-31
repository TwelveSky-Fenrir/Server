using System.Diagnostics;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.Commerce;

public sealed class ProxyShopBootReloadHost(
    ZoneRegistry zones,
    IOfflineShopRepository offlineShops,
    ILogger<ProxyShopBootReloadHost> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Proxy-shop boot reload failed -- open (ShopState=1) proxy shops stay invisible in-world on this " +
                "shard until the next restart, while remaining purchasable through the database-backed search " +
                "path. The shard still starts.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var listings = await offlineShops.GetAllOpenAsync(ct).ConfigureAwait(false);

        var owners = new Dictionary<int, string>();
        foreach (var row in listings)
            owners[row.CharacterId] = row.AvatarName;

        if (owners.Count == 0)
        {
            logger.LogInformation(
                "Proxy-shop boot reload: no open proxy shop with items found cluster-wide; nothing to re-register");
            return;
        }

        var today = GameDate.Today();
        var registered = 0;
        var expired = 0;
        var foreign = 0;
        var skipped = 0;

        foreach (var (characterId, ownerName) in owners)
        {
            ct.ThrowIfCancellationRequested();

            OfflineShopRowDto? shop;
            try
            {
                (shop, _) = await offlineShops.GetByCharacterAsync(characterId, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Proxy-shop boot reload: header read failed for character {CharacterId}; that stall stays unloaded",
                    characterId);
                skipped++;
                continue;
            }

            if (shop is not { ShopState: 1 })
            {
                skipped++;
                continue;
            }

            if (shop.ShopDate < today)
            {
                if (await CloseExpiredAsync(characterId, ct).ConfigureAwait(false))
                    expired++;
                else
                    skipped++;

                continue;
            }

            if (shop.ZoneNumber is not { } zoneNumber)
            {
                foreign++;
                continue;
            }

            if (!zones.TryGet(zoneNumber, out var zone))
            {
                foreign++;
                continue;
            }

            if (zone.ProxyShopCount >= Zone.MaxProxyShopSlots)
            {
                logger.LogWarning(
                    "Proxy-shop boot reload: zone {MapId} hit the {Cap}-slot ceiling; character {CharacterId} and " +
                    "any further stall on that map stay unloaded",
                    zone.MapId, Zone.MaxProxyShopSlots, characterId);
                skipped++;
                continue;
            }

            zone.RegisterProxyShop(new ProxyShopBroadcastEntry(characterId, unchecked(characterId * 2 + 1),
                ownerName, shop.ShopName, shop.LocationX, shop.LocationY, shop.LocationZ, shop.ShopDate));
            registered++;
        }

        logger.LogInformation(
            "Proxy-shop boot reload: {Registered} stall(s) re-registered, {Expired} swept as expired, {Foreign} " +
            "owned by another shard's maps, {Skipped} skipped, out of {Total} open shop(s) in {ElapsedMs} ms",
            registered, expired, foreign, skipped, owners.Count, sw.ElapsedMilliseconds);
    }

    private async Task<bool> CloseExpiredAsync(int characterId, CancellationToken ct)
    {
        try
        {
            await offlineShops.SetStateAsync(characterId, 0, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Proxy-shop boot reload: failed to close expired proxy shop for character {CharacterId}; it stays " +
                "purchasable through the search path",
                characterId);
            return false;
        }

        try
        {
            await offlineShops.SetProxyShopNameAsync(characterId, string.Empty, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Proxy-shop boot reload: display-name clear failed for character {CharacterId} after a successful " +
                "expiry close",
                characterId);
        }

        return true;
    }
}
