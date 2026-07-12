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
    string? SocketData);

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
    string? SocketData);

// ItemId is nullable (economy-hardening pass 2026-07-12): same defect/fix as AccountVaultItemSlotTvp.ItemId
// in Fenrir.Data.Abstractions.Accounts.AccountVaultDtos -- game.tvp_OfflineShopItemSlot.ItemId is SQL
// `INT NULL` with NULL meaning "empty slot" (Database/Tables/game/OfflineShopItems.sql's own header
// comment), but this TVP record declared a non-nullable `int`, making that NULL structurally
// unrepresentable through this TVP even though the read-side sibling (OfflineShopItemRowDto.ItemId above)
// was already correctly nullable. See .claude/agent-memory/fenrir-database-engineer/
// tvp_itemid_nullability_mismatch_2026-07-12.md for the full citation trail.
[GenerateTvp(Schema = "game", TvpName = "tvp_OfflineShopItemSlot")]
public sealed partial record OfflineShopItemSlotTvp(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int Price,
    string? SocketData);
