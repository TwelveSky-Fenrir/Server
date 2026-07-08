namespace Fenrir.Application.Login.Abstractions.ChangeMousePin;

public enum ChangeMousePinOutcome
{
    NoPinConfigured,
    InvalidFormat,
    WrongPassword,
    StorageFailure,
    Success,

    /// <summary>
    ///     Fenrir-only addition, no legacy analog: the account-scoped, cross-reconnect PIN lockout
    ///     (auth.AccountPins.LockedUntilUtc, Migrations/028_account_pin_lockout.sql) is currently active.
    ///     Never counted as a further attempt against the lockout counter itself.
    /// </summary>
    Locked
}

public readonly record struct ChangeMousePinResult(ChangeMousePinOutcome Outcome);

public interface IChangeMousePinService
{
    public ValueTask<ChangeMousePinResult> ChangeMousePinAsync(int accountId, string currentPin, string newPin,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Records a game.EventLog AccountSecurity row for one rejected mouse-PIN CHANGE attempt (op14).
    ///     Mirrors <see cref="Fenrir.Application.Login.Abstractions.VerifyMousePin.IVerifyMousePinService.LogFailedAttemptAsync" />
    ///     exactly -- see that method's own remarks for the full rationale. Added to close a Fenrir-authored
    ///     asymmetry: without this, an attacker guessing the PIN entirely through op14 (which shares the
    ///     exact same LoginClientSession.PinFailureCount counter and 3-strike disconnect as op15) left zero
    ///     durable trace, while the identical guessing pattern through op15 was already fully audited.
    /// </summary>
    public ValueTask LogFailedAttemptAsync(int accountId, int failureCount, bool lockedOut,
        CancellationToken cancellationToken);
}
