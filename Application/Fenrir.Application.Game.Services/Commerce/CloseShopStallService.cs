using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Commerce;

public sealed class CloseShopStallService(IOfflineShopRepository offlineShops) : ICloseShopStallService
{
    public CloseShopStallResponse? CloseLiveShop(PlayerRuntimeState state)
    {
        if (!state.PshopOpen)
            return null;

        state.PshopOpen = false;
        return new CloseShopStallResponse { Result = 1 };
    }

    public async ValueTask CloseOfflineShopAsync(int characterId, CancellationToken cancellationToken)
    {
        await offlineShops.SetStateAsync(characterId, 0, cancellationToken);
    }
}
