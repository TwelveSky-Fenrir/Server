using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class CloseShopStallService(IOfflineShopRepository offlineShops, ILogger<CloseShopStallService> logger)
    : ICloseShopStallService
{
    public async ValueTask CloseLiveShopAsync(PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            using var shopLease = await PersonalShopBusinessLock.AcquireAsync(state.CharacterId, cancellationToken);
            if (!state.PshopOpen)
            {
                logger.LogDebug("Close personal shop rejected: character {CharacterId} has no personal shop open",
                    state.CharacterId);
                return;
            }

            if (!await zone.PostPshopCommandAndWaitAsync(new PshopZoneCommand(state.CharacterId, true),
                    cancellationToken))
            {
                logger.LogError(
                    "Zone {MapId} pshop inbox full: failed manual shop-close mirror for character {CharacterId}",
                    zone.MapId, state.CharacterId);
                return;
            }

            logger.LogInformation("Personal shop closed: character {CharacterId}", state.CharacterId);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    public async ValueTask CloseOfflineShopAsync(int characterId, Zone zone, CancellationToken cancellationToken)
    {
        using var shopLease = await PersonalShopBusinessLock.AcquireAsync(characterId, cancellationToken);
        var (shop, _) = await offlineShops.GetByCharacterAsync(characterId, cancellationToken);
        if (shop is not { ShopState: 1 })
        {
            logger.LogDebug("Close proxy shop rejected: character {CharacterId} has no proxy shop open", characterId);
            return;
        }

        await offlineShops.SetStateAsync(characterId, 0, cancellationToken);
        zone.RemoveProxyShop(characterId);
        logger.LogInformation(
            "Proxy shop closed: character {CharacterId} (items/money remain attached to the shop for later retrieval/withdrawal)",
            characterId);
    }
}
