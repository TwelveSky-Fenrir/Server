using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Application.Login.Domain.Pins;
using Fenrir.Application.Login.Services.AccountSecurity;
using Fenrir.Data.Security;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.ChangeMousePin;

public sealed class ChangeMousePinService(
    IAccountPinRepository pins,
    IEventLogRepository eventLog,
    ILogger<ChangeMousePinService> logger)
    : IChangeMousePinService
{
    public async ValueTask<ChangeMousePinResult> ChangeMousePinAsync(int accountId, string currentPin, string newPin,
        CancellationToken cancellationToken)
    {
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
            return new ChangeMousePinResult(ChangeMousePinOutcome.NoPinConfigured);

        if (storedPin.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            logger.LogWarning(
                "PIN change rejected: account {AccountId} is locked out until {LockedUntilUtc} after {FailedAttempts} cumulative mismatches",
                accountId, lockedUntil, storedPin.FailedAttempts);
            await eventLog.LogAsync(AccountSecurityEventCodes.MousePinAttemptRejectedLocked,
                EventLogCategory.AccountSecurity, accountId, null, null, null, null, null, null, null, null,
                (byte)Math.Min(storedPin.FailedAttempts, byte.MaxValue), null, cancellationToken);
            return new ChangeMousePinResult(ChangeMousePinOutcome.Locked);
        }

        if (!MousePinFormat.IsValid(currentPin) || !MousePinFormat.IsValid(newPin))
            return new ChangeMousePinResult(ChangeMousePinOutcome.InvalidFormat);

        var currentPinOk = PasswordHasher.Verify(currentPin, storedPin.PinHash, storedPin.PinSalt);

        if (!currentPinOk)
        {
            await pins.RecordAttemptAsync(accountId, false, cancellationToken);
            return new ChangeMousePinResult(ChangeMousePinOutcome.WrongPassword);
        }

        try
        {
            var (hash, salt) = PasswordHasher.Hash(newPin);
            await pins.SetAsync(accountId, hash, salt, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PIN change storage failed for account {AccountId}", accountId);
            return new ChangeMousePinResult(ChangeMousePinOutcome.StorageFailure);
        }

        await pins.RecordAttemptAsync(accountId, true, cancellationToken);

        return new ChangeMousePinResult(ChangeMousePinOutcome.Success);
    }

        public async ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken)
    {
        var eventCode = lockedOut
            ? AccountSecurityEventCodes.MousePinChangeLockout
            : AccountSecurityEventCodes.MousePinChangeMismatch;

        if (lockedOut)
            logger.LogWarning(
                "Account {AccountId} locked out after {FailureCount} consecutive mouse-PIN CHANGE mismatches",
                accountId, failureCount);
        else
            logger.LogWarning("Account {AccountId} mouse-PIN CHANGE mismatch (attempt {FailureCount})", accountId,
                failureCount);

        await eventLog.LogAsync(eventCode, EventLogCategory.AccountSecurity, accountId, null, null, null, null,
            null, null, null, null, (byte)failureCount, null, cancellationToken);
    }
}
