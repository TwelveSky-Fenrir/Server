using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Accounts;

[GenerateDto]
public sealed partial record AccountVaultBalanceDto(
    int AccountId,
    long Money,
    long Money2,
    DateTime UpdatedAtUtc,
    int BigMoney = 0,
    long Revision = 0);

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

public readonly record struct AccountVaultCharacterItemSnapshot(
    int ItemId,
    int Quantity,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int ExpireDate,
    int Serial,
    byte XPos,
    byte YPos);

public readonly record struct AccountVaultItemSnapshot(
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int ExpireDate);

public readonly record struct AccountVaultCharacterSlotMutation(
    byte Slot,
    AccountVaultCharacterItemSnapshot? Expected,
    AccountVaultCharacterItemSnapshot? Replacement);

public readonly record struct AccountVaultItemSlotMutation(
    short SlotIndex,
    AccountVaultItemSnapshot? Expected,
    AccountVaultItemSnapshot? Replacement);
