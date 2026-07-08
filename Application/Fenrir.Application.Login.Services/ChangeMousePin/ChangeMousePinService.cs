using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Application.Login.Domain.Pins;
using Fenrir.Application.Login.Services.AccountSecurity;
using Fenrir.Data.Security;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.ChangeMousePin;

/// <summary>
///     op14 CL_CHANGE_MOUSE_PASSWORD_SEND business logic: verifies the current PIN then stores the new one.
///     Every mismatch (and the account-scoped lockout it can trigger) is recorded exactly like op15
///     VerifyMousePinService -- see <see cref="LogFailedAttemptAsync" />'s own remarks for why this
///     project-authored symmetry matters even though legacy itself never audited either opcode's mismatch.
/// </summary>
public sealed class ChangeMousePinService(
    IAccountPinRepository pins,
    IEventLogRepository eventLog,
    ILogger<ChangeMousePinService> logger)
    : IChangeMousePinService
{
    public async ValueTask<ChangeMousePinResult> ChangeMousePinAsync(int accountId, string currentPin, string newPin,
        CancellationToken cancellationToken)
    {
        // No stored PIN => caller must create one first (op13).
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
            return new ChangeMousePinResult(ChangeMousePinOutcome.NoPinConfigured);

        // Fenrir-only account-scoped, cross-reconnect lockout (Migrations/028_account_pin_lockout.sql) --
        // checked before format/hash so a locked-out account never even reaches PasswordHasher.Verify. See
        // VerifyMousePinService's identical check and the pincode-second-password audit's Major finding.
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

        // Every real comparison (success or failure) is reported to the account-scoped lockout counter --
        // the Fenrir-only analog of LoginService.AuthenticateConstantTimeAsync's own
        // accounts.RecordLoginAttemptAsync call for the primary password. A successful change additionally
        // resets the counter here (rather than relying on usp_AccountPin_Set, which never touches these
        // columns) so a legitimate PIN change always clears any accumulated lockout state.
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
            // Legacy: storage failure replies 2 without disconnecting (S04_MyWork02.cpp l.525-530). Previously
            // swallowed with no trace at all -- logged here (the only place the exception itself is still in
            // scope) so a real PIN-storage failure is diagnosable instead of vanishing silently.
            logger.LogWarning(ex, "PIN change storage failed for account {AccountId}", accountId);
            return new ChangeMousePinResult(ChangeMousePinOutcome.StorageFailure);
        }

        await pins.RecordAttemptAsync(accountId, true, cancellationToken);

        // Success is announced by ChangeMousePinHandler (matching DeleteAvatarService/CreateAvatarService's
        // own posture: this service layer only logs the failure/exception path it alone can see).
        return new ChangeMousePinResult(ChangeMousePinOutcome.Success);
    }

    /// <summary>
    ///     Records a game.EventLog AccountSecurity row for one rejected mouse-PIN CHANGE attempt (op14) --
    ///     the op14 mirror of VerifyMousePinService.LogFailedAttemptAsync. Closes a Fenrir-authored gap
    ///     (not a legacy-parity regression -- legacy's own GL_502_MISMATCH_MOUSE_PASSWORD call for this
    ///     branch is dead/commented code too, S04_MyWork02.cpp l.514): without this, an attacker could
    ///     brute-force the shared LoginClientSession.PinFailureCount counter entirely through op14 and leave
    ///     no durable audit trail, while the identical guessing pattern through op15 was already fully
    ///     audited.
    /// </summary>
    public async ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken)
    {
        var eventCode = lockedOut
            ? AccountSecurityEventCodes.MousePinChangeLockout
            : AccountSecurityEventCodes.MousePinChangeMismatch;

        // Operational (Aspire dashboard/OTLP) log for live observability, distinct from the durable
        // game.EventLog AccountSecurity row written just below -- mirrors VerifyMousePinService's own split.
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
