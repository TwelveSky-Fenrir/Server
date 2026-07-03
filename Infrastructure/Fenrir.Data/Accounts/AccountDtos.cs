using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Accounts;

/// <summary>
///     Ordinal contract (I-04) for auth.usp_Account_Authenticate's result set: AccountId, PasswordHash, PasswordSalt,
///     FailedLoginCount, LockoutUntilUtc, IsBanned, in that exact column order. The Argon2id verdict and lockout
///     policy stay in Fenrir.Domain/the LoginServer (§9.1) -- the proc only ever reads and reports state.
/// </summary>
[GenerateDto]
public sealed partial record AuthenticateAccountDto(
    int AccountId,
    byte[] PasswordHash,
    byte[] PasswordSalt,
    int FailedLoginCount,
    DateTime? LockoutUntilUtc,
    bool IsBanned);

/// <summary>
///     Ordinal contract (I-04) for auth.usp_AccountPin_Get's result set: PinHash, PinSalt, in that exact column
///     order. Absence of a row = the account has no mouse PIN yet (op 13 create path); like the password, the
///     Argon2id verdict on a PIN attempt lives in C# (Fenrir.Domain.Security.PasswordHasher), never in SQL.
/// </summary>
[GenerateDto]
public sealed partial record AccountPinDto(
    byte[] PinHash,
    byte[] PinSalt);
