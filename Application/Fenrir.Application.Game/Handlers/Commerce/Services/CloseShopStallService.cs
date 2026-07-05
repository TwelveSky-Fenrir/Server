using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Commerce;

namespace Fenrir.Application.Game.Handlers.Commerce.Services;

/// <summary>Business logic for CZ_END_PSHOP_SEND (opcode 32), extracted from <see cref="CloseShopStallHandler" />.</summary>
public interface ICloseShopStallService
{
    /// <summary>
    ///     Closes the caller's LIVE personal shop, if one is open. Returns <c>null</c> when nothing was open (no
    ///     reply is sent in that case), matching the legacy.
    /// </summary>
    CloseShopStallResponse? CloseLiveShop(PlayerRuntimeState state);

    /// <summary>
    ///     Closes the offline/deputy shop (ShopState only -- items/money stay attached). No unicast reply is ever
    ///     sent for this, matching the legacy.
    /// </summary>
    ValueTask CloseOfflineShopAsync(int characterId, CancellationToken cancellationToken);
}

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
