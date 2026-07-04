using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Accounts;

// auth.usp_Account_Authenticate; ordinal-mapped. Proc only reads/reports state -- Argon2id verdict & lockout policy live in C#.
[GenerateDto]
public sealed partial record AuthenticateAccountDto(
    int AccountId,
    byte[] PasswordHash,
    byte[] PasswordSalt,
    int FailedLoginCount,
    DateTime? LockoutUntilUtc,
    bool IsBanned);

// auth.usp_AccountPin_Get; ordinal-mapped. No row = account has no PIN yet; Argon2id verdict lives in C#, never SQL.
[GenerateDto]
public sealed partial record AccountPinDto(
    byte[] PinHash,
    byte[] PinSalt);
