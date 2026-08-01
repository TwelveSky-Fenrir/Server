using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Social.Pshop;

public readonly record struct PshopZoneCommand(
    int CharacterId,
    int Page,
    int Slot,
    bool CloseShop,
    BuyShopItemResponse SellerSoldNotification,
    TaskCompletionSource? Applied = null);
