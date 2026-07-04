using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Commerce;

/// <summary>
///     game.Characters' 7-day login-reward cursor -- ordinal contract of usp_Character_GetRewardClaimState:
///     RewardClaimDay (0..7, 7 = fully claimed), RewardClaimDate (YYYYMMDD int, 0 = never claimed).
/// </summary>
[GenerateDto]
public sealed partial record RewardClaimStateDto(byte RewardClaimDay, int RewardClaimDate);

/// <summary>
///     One game.OfflineShops row -- ordinal contract of usp_OfflineShop_GetByCharacter's RS0 /
///     usp_OfflineShop_OpenAndReplaceContainers' implicit shape. ZoneNumber is nullable (see that table's
///     own header comment).
/// </summary>
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
///     One populated game.OfflineShopItems slot -- ordinal contract of usp_OfflineShop_GetByCharacter's
///     RS1. ItemId is NULL only for a row this schema would never actually persist (see
///     game.tvp_OfflineShopItemSlot's own "one row per occupied slot" convention) -- kept nullable purely
///     for defensive parity with the underlying column.
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

/// <summary>
///     Read-only shape for usp_OfflineShop_OpenAndReplaceContainers/SetItems' TVP parameter -- mirrors
///     game.tvp_OfflineShopItemSlot's column order 1:1, one row per OCCUPIED shop slot (CharacterId is a
///     proc scalar, not a TVP column). ItemId is never actually 0/absent in a row this application
///     constructs -- an empty slot is simply omitted from the list entirely, same convention as
///     <c>Fenrir.Data.Characters.CharacterItemSlotTvp</c>.
/// </summary>
[GenerateTvp(Schema = "game", TvpName = "tvp_OfflineShopItemSlot")]
public sealed partial record OfflineShopItemSlotTvp(
    short SlotIndex,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int Price,
    string? SocketData);
