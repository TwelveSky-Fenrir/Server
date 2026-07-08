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
    ///     A successful reply carries <c>Result=0</c> -- distinct from <see cref="PurchaseAsync" />'s
    ///     <c>Result=1000</c> on success, the same opcode/packet legacy asymmetry
    ///     <see cref="IOpenShopStallService" />'s personal/proxy 0-vs-100 split already preserves; do not
    ///     normalize both variants to the same success code. Returns <c>null</c> when the caller should
    ///     abort the session as faulted.
    /// </summary>
    /// <param name="accountId">
    ///     The acting player's account id -- carried only for the game.EventLog audit row written once
    ///     persistence succeeds (legacy <c>GL_1001_PXSHOP_ITEM</c>, action "Retrieved"); not used for any
    ///     validation or persistence decision.
    /// </param>
    public ValueTask<UpdateProxyShopResponse?> RetrieveAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken);

    /// <summary>
    ///     <c>BuySort</c> 2 -- PURCHASE from another character's open shop. Only the buyer/retriever is ever a
    ///     live participant -- the seller's shop lives purely in SQL, so no dual-lock is needed here (unlike
    ///     <c>BuyShopItemService</c>'s live-PShop twin). A successful reply carries <c>Result=1000</c> --
    ///     never <c>Result=0</c>, which is reserved for <see cref="RetrieveAsync" />'s own success (see that
    ///     method's remarks). Returns <c>null</c> when the caller should abort the session as faulted.
    /// </summary>
    /// <param name="accountId">
    ///     The buyer's account id -- carried only for the game.EventLog audit row written once persistence
    ///     succeeds (legacy <c>GL_1001_PXSHOP_ITEM</c>, action "Purchased"); not used for any validation or
    ///     persistence decision.
    /// </param>
    public ValueTask<UpdateProxyShopResponse?> PurchaseAsync(UpdateProxyShopRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, short slotIndex, ItemDefinition itemDefinition,
        CancellationToken cancellationToken);
}
