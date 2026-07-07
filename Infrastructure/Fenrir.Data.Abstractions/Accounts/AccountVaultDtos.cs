using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Accounts;

/// <summary>
///     usp_AccountVault_Get/usp_AccountVault_TransferMoneyWithCharacter RS0 -- the account's shared vault/bank
///     money pools (legacy masterinfo.uSaveMoney/uSaveMoney2). Only <see cref="Money" /> currently has a live
///     writer (<see cref="IAccountVaultRepository.TransferMoneyWithCharacterAsync" />, CZ_PROCESS_DATA_SEND
///     tSort 231/232); <see cref="Money2" /> mirrors the table's second column but has no known Fenrir-side
///     consumer yet.
/// </summary>
[GenerateDto]
public sealed partial record AccountVaultBalanceDto(int AccountId, long Money, long Money2, DateTime UpdatedAtUtc);

/// <summary>
///     usp_AccountVault_Get RS1 -- one row per occupied vault slot (legacy masterinfo.uSaveItem, 28 slots,
///     MAX_SAVE_ITEM_SLOT_NUM). <see cref="Value" /> is the same packed Enchant/Combine/Refine/Socket int
///     every other item-row DTO in this codebase uses (<c>Fenrir.Application.Game.Domain.World.Loot.ItemValueCodec</c>).
///     <see cref="SocketData" /> has no established encoding anywhere in this codebase yet (same posture as
///     <c>OfflineShopItemRowDto.SocketData</c>) -- gem sockets are not preserved through the vault.
/// </summary>
[GenerateDto]
public sealed partial record AccountVaultItemSlotDto(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData);

// Mirrors game.tvp_AccountVaultItemSlot order; AccountId is a proc scalar, not a TVP column. One row per
// occupied slot -- empty slots are omitted, not zero-filled (same convention as CharacterItemSlotTvp/
// OfflineShopItemSlotTvp).
[GenerateTvp(Schema = "game", TvpName = "tvp_AccountVaultItemSlot")]
public sealed partial record AccountVaultItemSlotTvp(
    short SlotIndex,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData);
