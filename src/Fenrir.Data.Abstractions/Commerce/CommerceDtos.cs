using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Commerce;

[GenerateDto]
public sealed partial record RewardClaimStateDto(byte RewardClaimDay, int RewardClaimDate);

[GenerateDto]
public sealed partial record OfflineShopRowDto(
    int CharacterId,
    short? ZoneNumber,
    byte ShopState,
    int ShopDate,
    int Money,
    int BigMoney,
    int LocationX,
    int LocationY,
    int LocationZ,
    string ShopName);

[GenerateDto]
public sealed partial record OfflineShopItemRowDto(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int Price,
    string? SocketData,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3);

[GenerateDto]
public sealed partial record ProxyShopNameRowDto(int CharacterId, string ShopName);

[GenerateDto]
public sealed partial record OfflineShopOpenListingRowDto(
    int CharacterId,
    string AvatarName,
    short SlotIndex,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int Price,
    string? SocketData,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3);

[GenerateTvp(Schema = "game", TvpName = "tvp_OfflineShopItemSlot")]
public sealed partial record OfflineShopItemSlotTvp(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int Price,
    string? SocketData,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3);

public readonly record struct OfflineShopListingKey(
    short SlotIndex,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3);
