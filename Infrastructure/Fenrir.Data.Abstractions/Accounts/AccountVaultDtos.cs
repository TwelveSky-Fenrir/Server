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

// ItemId is nullable (economy-hardening pass 2026-07-12): game.tvp_AccountVaultItemSlot.ItemId is SQL
// `INT NULL` and NULL is a meaningful, documented value here (0 in the packed legacy source translates to
// NULL = empty slot, per usp_AccountVault_SetItems.sql's own header comment) -- a non-nullable `int` here
// meant CaeriusNet's generated TVP-row binding could structurally never emit SQL NULL for this column
// (confirmed via decompiling CaeriusNet.Generator.dll: the SetDBNull branch is only emitted for a
// nullable-annotated property), silently making "empty slot" unrepresentable through this TVP even though
// every other layer (the read-side AccountVaultItemSlotDto.ItemId two lines above, the table column, the
// consuming procedure's own contract) already treats it as nullable. Latent until now: every real call site
// happens to omit empty slots entirely rather than sending an explicit null-ItemId row, so this never
// actually triggered an FK violation -- fixed proactively rather than waiting for a caller to rely on the
// documented contract.
[GenerateTvp(Schema = "game", TvpName = "tvp_AccountVaultItemSlot")]
public sealed partial record AccountVaultItemSlotTvp(
    short SlotIndex,
    int? ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    string? SocketData);
