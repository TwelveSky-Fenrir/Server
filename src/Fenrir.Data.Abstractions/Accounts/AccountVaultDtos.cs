using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Accounts;

[GenerateDto]
public sealed partial record AccountVaultBalanceDto(
    int AccountId,
    long Money,
    long Money2,
    DateTime UpdatedAtUtc,
    int BigMoney = 0);

[GenerateDto]
public sealed partial record AccountVaultItemSlotDto(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData);

[GenerateTvp(Schema = "game", TvpName = "tvp_AccountVaultItemSlot")]
public sealed partial record AccountVaultItemSlotTvp(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData);

// Ordinal-mappe : ces quatre parametres finaux exigent que usp_AccountVault_GetV2 les projette dans cet
// ordre, en fin de SELECT. Les gemmes sont trois et contigues (uSaveSocket[28][3], STRUCT.h:462).
[GenerateDto]
public sealed partial record AccountVaultItemSlotV2Dto(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int ExpireDate);

[GenerateTvp(Schema = "game", TvpName = "tvp_AccountVaultItemSlotV2")]
public sealed partial record AccountVaultItemSlotV2Tvp(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int ExpireDate);
