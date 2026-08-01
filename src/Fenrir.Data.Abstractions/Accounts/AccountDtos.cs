using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Accounts;

[GenerateDto]
public sealed partial record AuthenticateAccountDto(
    int AccountId,
    byte[] PasswordHash,
    byte[] PasswordSalt,
    int FailedLoginCount,
    DateTime? LockoutUntilUtc,
    bool IsBanned,
    short AccountGrade = 0);

[GenerateDto]
public sealed partial record AccountPinDto(
    byte[] PinHash,
    byte[] PinSalt,
    int FailedAttempts = 0,
    DateTime? LockedUntilUtc = null);
