using Fenrir.Application.Game.Domain.Social.Pshop;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Abstractions.Commerce;

public enum BuyShopItemSellerOutcome
{
    Abort,

    Reply,

    Proceed,

    ProxyProceed
}

public readonly record struct BuyShopItemSellerResult(
    BuyShopItemSellerOutcome Outcome,
    BuyShopItemResponse? Reply,
    PlayerRuntimeState? Seller,
    PshopPurchasePolicy.SlotView Slot,
    int ProxySellerId = 0);

public readonly record struct BuyShopItemCommitResult(
    bool Abort,
    BuyShopItemResponse? Response,
    ViewShopStallResponse? ListingRefresh,
    int? SellerCharacterId = null,
    BuyShopItemResponse? SellerSoldNotification = null,
    bool CloseSellerShop = false,
    int? ProxyShopToRemove = null);

public interface IBuyShopItemService
{
    public ValueTask<BuyShopItemSellerResult> FindSellerAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, int buyerId, CancellationToken cancellationToken);

    public ValueTask<BuyShopItemCommitResult> CommitAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer,
        PlayerRuntimeState seller, PshopPurchasePolicy.SlotView slot, CancellationToken cancellationToken);

    public ValueTask<BuyShopItemCommitResult> CommitProxyPurchaseAsync(BuyShopItemRequest packet, Zone zone,
        PlayerRuntimeState buyer, int sellerId, int accountId, PshopPurchasePolicy.SlotView slot,
        CancellationToken cancellationToken);
}
