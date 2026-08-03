using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Social.Pshop;

public readonly record struct PshopZoneCommand(
    int CharacterId,
    bool CloseShop,
    int? Page = null,
    int? Slot = null,
    BuyShopItemResponse? SellerSoldNotification = null,
    TaskCompletionSource? Applied = null);
