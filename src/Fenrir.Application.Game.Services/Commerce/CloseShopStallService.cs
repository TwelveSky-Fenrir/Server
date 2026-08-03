using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class CloseShopStallService(IOfflineShopRepository offlineShops, ILogger<CloseShopStallService> logger)
    : ICloseShopStallService
{
    public async ValueTask CloseLiveShopAsync(PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!state.PshopOpen)
        {
            logger.LogDebug("Close personal shop ignored: character {CharacterId} has no personal shop open",
                state.CharacterId);
            return;
        }

        if (!await zone.PostPshopCommandAndWaitAsync(new PshopZoneCommand(state.CharacterId, true),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} pshop inbox full: dropped manual shop-close mirror for character {CharacterId}",
                zone.MapId, state.CharacterId);

        logger.LogInformation("Personal shop closed: character {CharacterId}", state.CharacterId);
    }

    public async ValueTask CloseOfflineShopAsync(int characterId, Zone zone, CancellationToken cancellationToken)
    {
        await offlineShops.SetStateAsync(characterId, 0, cancellationToken);
        zone.RemoveProxyShop(characterId);
        logger.LogInformation(
            "Proxy shop closed: character {CharacterId} (items/money remain attached to the shop for later retrieval/withdrawal)",
            characterId);
    }
}
