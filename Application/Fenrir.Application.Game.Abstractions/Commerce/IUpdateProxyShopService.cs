using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public readonly record struct UpdateProxyShopValidation(bool Abort, short SlotIndex, ItemDefinition? ItemDefinition);

/// <summary>
///     Business logic for CZ_SET_DEPUTY_PSHOP_SEND (opcode 109), extracted from <see cref="UpdateProxyShopHandler" />
///     .
/// </summary>
public interface IUpdateProxyShopService
{
    /// <summary>Shared pre-lock validation for both <c>BuySort</c> branches.</summary>
    public UpdateProxyShopValidation Validate(UpdateProxyShopRequest packet);

    /// <summary>
    ///     <c>BuySort</c> 1 -- RETRIEVE an unsold item from the caller's own closed shop back to inventory.
    ///     Returns <c>null</c> when the caller should abort the session as faulted.
    /// </summary>
    public ValueTask<UpdateProxyShopResponse?> RetrieveAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>BuySort</c> 2 -- PURCHASE from another character's open shop. Only the buyer/retriever is ever a
    ///     live participant -- the seller's shop lives purely in SQL, so no dual-lock is needed here (unlike
    ///     <c>BuyShopItemService</c>'s live-PShop twin). Returns <c>null</c> when the caller should abort the
    ///     session as faulted.
    /// </summary>
    public ValueTask<UpdateProxyShopResponse?> PurchaseAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken);
}
