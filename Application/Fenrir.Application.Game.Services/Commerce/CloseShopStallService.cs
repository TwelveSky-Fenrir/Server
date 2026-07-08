using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class CloseShopStallService(IOfflineShopRepository offlineShops, ILogger<CloseShopStallService> logger)
    : ICloseShopStallService
{
    public CloseShopStallResponse? CloseLiveShop(PlayerRuntimeState state)
    {
        if (!state.PshopOpen)
        {
            logger.LogDebug("Close personal shop ignored: character {CharacterId} has no personal shop open",
                state.CharacterId);
            return null;
        }

        state.PshopOpen = false;
        logger.LogInformation("Personal shop closed: character {CharacterId}", state.CharacterId);
        return new CloseShopStallResponse { Result = 1 };
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
