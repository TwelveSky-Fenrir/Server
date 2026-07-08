using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Social.Pshop;

/// <summary>
///     Posted after a purchase already durably committed, to mirror the sold slot's clearing (and stall
///     close, if last item) onto the seller's live PlayerRuntimeState, and to deliver the seller-facing wire
///     notifications the purchase itself never reaches without this hop: B_BUY_PSHOP_RECV(6) "your item
///     sold", B_DEMAND_PSHOP_RECV(3) self-view listing refresh, and -- if that was the stall's last occupied
///     slot -- B_END_PSHOP_RECV(1) "your stall closed" plus an avatar-action broadcast to nearby players
///     (Server/ts25zone/S04_MyWork02.cpp:7067-7071,7096-7100,7102-7120). The purchase's real correctness
///     guard is the live Inventory re-validation under the dual EconomyActionLock; this only mirrors the
///     already-committed outcome and notifies. Callers must use <see cref="Zone.PostPshopCommandAndWaitAsync" />
///     (never the bare <see cref="Zone.PostPshopCommand" />) so a subsequent read of the seller's
///     PshopListing (e.g. to build the buyer-facing listing-refresh response) observes the post-clear state.
/// </summary>
/// <param name="CharacterId">The seller -- a no-op if they already left this zone.</param>
/// <param name="Page">The seller-side shop page the sold slot lived in.</param>
/// <param name="Slot">The seller-side shop index the sold slot lived in.</param>
/// <param name="CloseShop">True when this was the stall's last remaining item -- also clears PshopOpen.</param>
/// <param name="SellerSoldNotification">
///     The already-built B_BUY_PSHOP_RECV(6) "your item sold" response for the seller's own connection,
///     carrying the seller's own source slot coordinates and the sold item's value/socket details -- built
///     by the caller (the same <c>BuildReply</c> the buyer's own Result=0 notification uses) so wire-packet
///     construction stays centralized in the Services layer.
/// </param>
/// <param name="Applied">Overwritten by <see cref="Zone.PostPshopCommandAndWaitAsync" /> -- leave at default.</param>
public readonly record struct PshopZoneCommand(
    int CharacterId,
    int Page,
    int Slot,
    bool CloseShop,
    BuyShopItemResponse SellerSoldNotification,
    TaskCompletionSource? Applied = null);
