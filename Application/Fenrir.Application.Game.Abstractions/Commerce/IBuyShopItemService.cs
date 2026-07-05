using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Commerce;

/// <summary>The three ways <see cref="BuyShopItemService.FindSeller" /> can leave <see cref="BuyShopItemHandler" />.</summary>
public enum BuyShopItemSellerOutcome
{
    /// <summary>A protocol-level fault -- the handler should abort the session.</summary>
    Abort,

    /// <summary>A well-formed rejection -- the handler should send <see cref="BuyShopItemSellerResult.Reply" /> as-is.</summary>
    Reply,

    /// <summary>
    ///     Seller/slot resolved -- the handler should acquire both economy locks and call
    ///     <see cref="IBuyShopItemService.CommitAsync" />.
    /// </summary>
    Proceed
}

public readonly record struct BuyShopItemSellerResult(
    BuyShopItemSellerOutcome Outcome,
    BuyShopItemResponse? Reply,
    PlayerRuntimeState? Seller,
    PshopPurchasePolicy.SlotView Slot);

public readonly record struct BuyShopItemCommitResult(bool Abort, BuyShopItemResponse? Response, bool ShopClosed);

/// <summary>Business logic for CZ_BUY_PSHOP_SEND (opcode 35), extracted from <see cref="BuyShopItemHandler" />.</summary>
public interface IBuyShopItemService
{
    /// <summary>
    ///     Pre-lock validation: resolves the named seller and the advertised slot from their live personal-shop
    ///     listing. Must run BEFORE either economy lock is acquired.
    /// </summary>
    public BuyShopItemSellerResult FindSeller(BuyShopItemRequest packet, Zone zone, PlayerRuntimeState buyer,
        int buyerId);

    /// <summary>Commits the purchase. Both participants' economy locks must already be held.</summary>
    public ValueTask<BuyShopItemCommitResult> CommitAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer,
        PlayerRuntimeState seller, PshopPurchasePolicy.SlotView slot, CancellationToken cancellationToken);
}
