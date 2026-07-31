using Fenrir.Security.Credentials;
using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Application.Login.Services.AccountSecurity;
using Fenrir.Domain.Login.Pins;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.VerifyMousePin;

public sealed class VerifyMousePinService(
    IAccountPinRepository pins,
    IEventLogRepository eventLog,
    ILogger<VerifyMousePinService> logger)
    : IVerifyMousePinService
{
    public async ValueTask<VerifyMousePinResult> VerifyMousePinAsync(int accountId, string mousePasswordInput,
        CancellationToken cancellationToken)
    {
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
            return new VerifyMousePinResult(VerifyMousePinOutcome.NoPinConfigured);

        if (storedPin.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            logger.LogWarning(
                "PIN verify rejected: account {AccountId} is locked out until {LockedUntilUtc} after {FailedAttempts} cumulative mismatches",
                accountId, lockedUntil, storedPin.FailedAttempts);
            await eventLog.LogAsync(AccountSecurityEventCodes.MousePinAttemptRejectedLocked,
                EventLogCategory.AccountSecurity, accountId, null, null, null, null, null, null, null, null,
                (byte)Math.Min(storedPin.FailedAttempts, byte.MaxValue), null, cancellationToken);
            return new VerifyMousePinResult(VerifyMousePinOutcome.Locked);
        }

        if (!MousePinFormat.IsValid(mousePasswordInput))
            return new VerifyMousePinResult(VerifyMousePinOutcome.InvalidFormat);

        var pinOk = PasswordHasher.Verify(mousePasswordInput, storedPin.PinHash, storedPin.PinSalt);

        await pins.RecordAttemptAsync(accountId, pinOk, cancellationToken);

        return new VerifyMousePinResult(pinOk ? VerifyMousePinOutcome.Success : VerifyMousePinOutcome.WrongPassword);
    }

    public async ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken)
    {
        var eventCode = lockedOut
            ? AccountSecurityEventCodes.MousePinLockout
            : AccountSecurityEventCodes.MousePinMismatch;

        if (lockedOut)
            logger.LogWarning(
                "Account {AccountId} locked out after {FailureCount} consecutive mouse-PIN mismatches", accountId,
                failureCount);
        else
            logger.LogWarning("Account {AccountId} mouse-PIN mismatch (attempt {FailureCount})", accountId,
                failureCount);

        await eventLog.LogAsync(eventCode, EventLogCategory.AccountSecurity, accountId, null, null, null, null,
            null, null, null, null, (byte)failureCount, null, cancellationToken);
    }
}
