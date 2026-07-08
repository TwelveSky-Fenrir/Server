using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>The four ways <see cref="BuyShopItemService.FindSellerAsync" /> can leave <see cref="BuyShopItemHandler" />.</summary>
public enum BuyShopItemSellerOutcome
{
    /// <summary>A protocol-level fault -- the handler should abort the session.</summary>
    Abort,

    /// <summary>A well-formed rejection -- the handler should send <see cref="BuyShopItemSellerResult.Reply" /> as-is.</summary>
    Reply,

    /// <summary>
    ///     A LIVE personal shop's seller/slot resolved -- the handler should acquire BOTH participants'
    ///     economy locks (smaller CharacterId first) and call <see cref="IBuyShopItemService.CommitAsync" />.
    /// </summary>
    Proceed,

    /// <summary>
    ///     A registered, currently-open OFFLINE/proxy shop's seller/slot resolved
    ///     (<see cref="BuyShopItemSellerResult.ProxySellerId" />) -- the seller has no live
    ///     <see cref="PlayerRuntimeState" /> to lock at all, so the handler should acquire only the BUYER's
    ///     own economy lock and call <see cref="IBuyShopItemService.CommitProxyPurchaseAsync" />.
    /// </summary>
    ProxyProceed
}

public readonly record struct BuyShopItemSellerResult(
    BuyShopItemSellerOutcome Outcome,
    BuyShopItemResponse? Reply,
    PlayerRuntimeState? Seller,
    PshopPurchasePolicy.SlotView Slot,
    int ProxySellerId = 0);

/// <summary>
///     <paramref name="ListingRefresh" /> is the buyer-facing B_DEMAND_PSHOP_RECV(0) snapshot of the
///     seller's shop listing AFTER the sold slot was cleared (and, if it was the last item, the stall
///     closed) -- null only when <paramref name="Abort" /> is set. The seller's own notifications (item-sold,
///     self-view listing refresh, stall-closed, AOI broadcast) are delivered separately by the zone tick
///     that processes the posted <see cref="PshopZoneCommand" />, never by this handler.
/// </summary>
public readonly record struct BuyShopItemCommitResult(
    bool Abort,
    BuyShopItemResponse? Response,
    ViewShopStallResponse? ListingRefresh);

/// <summary>Business logic for CZ_BUY_PSHOP_SEND (opcode 35), extracted from <see cref="BuyShopItemHandler" />.</summary>
public interface IBuyShopItemService
{
    /// <summary>
    ///     Pre-lock resolution: dispatch always attempts the OFFLINE/proxy shop first (only reachable when the
    ///     hosting zone is the proxy-shop hub AND a matching OPEN proxy shop is registered under the given
    ///     seller name), falling through to the named LIVE/personal-shop lookup otherwise. Must run BEFORE
    ///     either flavor's economy lock is acquired.
    /// </summary>
    public ValueTask<BuyShopItemSellerResult> FindSellerAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, int buyerId, CancellationToken cancellationToken);

    /// <summary>Commits a LIVE personal-shop purchase. Both participants' economy locks must already be held.</summary>
    public ValueTask<BuyShopItemCommitResult> CommitAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer,
        PlayerRuntimeState seller, PshopPurchasePolicy.SlotView slot, CancellationToken cancellationToken);

    /// <summary>
    ///     Commits a purchase from a registered, currently-open OFFLINE/proxy shop. Only the BUYER's own
    ///     economy lock must already be held -- the seller need not be (and, per the whole point of a proxy
    ///     shop, usually is not) online at all.
    /// </summary>
    public ValueTask<BuyShopItemCommitResult> CommitProxyPurchaseAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, int sellerId, int accountId, PshopPurchasePolicy.SlotView slot,
        CancellationToken cancellationToken);
}
