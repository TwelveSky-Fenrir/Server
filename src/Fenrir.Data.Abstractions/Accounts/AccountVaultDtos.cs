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
