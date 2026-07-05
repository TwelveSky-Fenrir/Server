using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Commerce;

/// <summary>
///     usp_Character_GetRewardClaimState; RewardClaimDay 7 = fully claimed, RewardClaimDate 0 = never claimed
///     (YYYYMMDD int).
/// </summary>
[GenerateDto]
public sealed partial record RewardClaimStateDto(byte RewardClaimDay, int RewardClaimDate);

/// <summary>usp_OfflineShop_GetByCharacter RS0; ZoneNumber nullable.</summary>
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

/// <summary>
///     usp_OfflineShop_GetByCharacter RS1; ItemId nullable only for defensive parity with the column (never actually
///     null in practice).
/// </summary>
[GenerateDto]
public sealed partial record OfflineShopItemRowDto(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int Price,
    string? SocketData);

/// <summary>One game.ProxyShopNames row -- ordinal contract of usp_ProxyShopName_GetByCharacter (0 or 1 row).</summary>
[GenerateDto]
public sealed partial record ProxyShopNameRowDto(int CharacterId, string ShopName);

// Mirrors game.tvp_OfflineShopItemSlot order; CharacterId is a proc scalar, not a TVP column. One row per occupied slot -- empty slots are omitted, not zero-filled.
[GenerateTvp(Schema = "game", TvpName = "tvp_OfflineShopItemSlot")]
public sealed partial record OfflineShopItemSlotTvp(
    short SlotIndex,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int Price,
    string? SocketData);
