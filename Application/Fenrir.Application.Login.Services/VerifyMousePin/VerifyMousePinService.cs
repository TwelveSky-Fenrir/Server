using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Application.Login.Domain.Pins;
using Fenrir.Application.Login.Services.AccountSecurity;
using Fenrir.Data.Security;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.VerifyMousePin;

/// <summary>op15 CL_LOGIN_MOUSE_PASSWORD_SEND business logic: verifies the stored PIN against the client's input.</summary>
public sealed class VerifyMousePinService(
    IAccountPinRepository pins,
    IEventLogRepository eventLog,
    ILogger<VerifyMousePinService> logger)
    : IVerifyMousePinService
{
    public async ValueTask<VerifyMousePinResult> VerifyMousePinAsync(int accountId, string mousePasswordInput,
        CancellationToken cancellationToken)
    {
        // No stored PIN => caller must create one first (op13).
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
            return new VerifyMousePinResult(VerifyMousePinOutcome.NoPinConfigured);

        // Fenrir-only account-scoped, cross-reconnect lockout (Migrations/028_account_pin_lockout.sql) --
        // checked before format/hash so a locked-out account never even reaches PasswordHasher.Verify.
        // See IVerifyMousePinService.LogFailedAttemptAsync's remarks and the pincode-second-password audit's
        // Major finding for the full rationale.
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

        // Every real comparison (success or failure) is reported to the account-scoped lockout counter --
        // the Fenrir-only analog of LoginService.AuthenticateConstantTimeAsync's own
        // accounts.RecordLoginAttemptAsync call for the primary password.
        await pins.RecordAttemptAsync(accountId, pinOk, cancellationToken);

        return new VerifyMousePinResult(pinOk ? VerifyMousePinOutcome.Success : VerifyMousePinOutcome.WrongPassword);
    }

    public async ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken)
    {
        var eventCode = lockedOut
            ? AccountSecurityEventCodes.MousePinLockout
            : AccountSecurityEventCodes.MousePinMismatch;

        // Operational (Aspire dashboard/OTLP) log for live observability, distinct from the durable
        // game.EventLog AccountSecurity row written just below -- see PacketLog's own remarks on that
        // audience split. LogWarning (not Debug): a wrong-PIN attempt, and especially a lockout, is exactly
        // the "rejected action due to a security gate" the project owner wants visible by default.
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
