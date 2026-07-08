using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>Business logic for CZ_END_PSHOP_SEND (opcode 32), extracted from <see cref="CloseShopStallHandler" />.</summary>
public interface ICloseShopStallService
{
    /// <summary>
    ///     Closes the caller's LIVE personal shop, if one is open. Returns <c>null</c> when nothing was open (no
    ///     reply is sent in that case), matching the legacy.
    /// </summary>
    public CloseShopStallResponse? CloseLiveShop(PlayerRuntimeState state);

    /// <summary>
    ///     Closes the offline/deputy shop (ShopState only -- items/money stay attached). No unicast reply is ever
    ///     sent for this, matching the legacy. Also removes it from <paramref name="zone" />'s periodic-broadcast
    ///     table (<see cref="Zone.RemoveProxyShop" />) so it stops being re-announced the very next tick.
    /// </summary>
    public ValueTask CloseOfflineShopAsync(int characterId, Zone zone, CancellationToken cancellationToken);
}
