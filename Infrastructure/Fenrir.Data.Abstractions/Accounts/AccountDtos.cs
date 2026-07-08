using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Accounts;

// auth.usp_Account_Authenticate; ordinal-mapped. Proc only reads/reports state -- Argon2id verdict & lockout policy live in C#.
// AccountGrade (Migrations/011_accounts_add_account_grade.sql) is appended last, defaulted so every
// pre-existing 6-arg test construction keeps compiling unchanged -- legacy uUserSort, "< 1" is the
// not-elevated gate (Server/ts25zone/S04_MyWork04.cpp:1489 and siblings).
[GenerateDto]
public sealed partial record AuthenticateAccountDto(
    int AccountId,
    byte[] PasswordHash,
    byte[] PasswordSalt,
    int FailedLoginCount,
    DateTime? LockoutUntilUtc,
    bool IsBanned,
    short AccountGrade = 0);

// auth.usp_AccountPin_Get; ordinal-mapped. No row = account has no PIN yet; Argon2id verdict lives in C#, never SQL.
// FailedAttempts/LockedUntilUtc (Migrations/028_account_pin_lockout.sql) are appended last, defaulted so
// every pre-existing 2-arg test construction keeps compiling unchanged -- the account-scoped, cross-
// reconnect PIN brute-force lockout, mirroring AuthenticateAccountDto's own append-at-the-end convention
// for AccountGrade.
[GenerateDto]
public sealed partial record AccountPinDto(
    byte[] PinHash,
    byte[] PinSalt,
    int FailedAttempts = 0,
    DateTime? LockedUntilUtc = null);
