using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Social.Pshop;

public readonly record struct PshopZoneCommand(
    int CharacterId,
    bool CloseShop,
    int? Page = null,
    int? Slot = null,
    BuyShopItemResponse? SellerSoldNotification = null,
    bool SendSellerListingRefresh = false,
    PshopInfo? OpenListing = null,
    bool DisableAutoHunt = false,
    bool BroadcastOpenAction = false,
    TaskCompletionSource? Applied = null);
