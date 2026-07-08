namespace Fenrir.Application.Login.Services.AccountSecurity;

/// <summary>
///     Locally-scoped game.EventLog.EventCode catalog for <see cref="EventLogCategory.AccountSecurity" />,
///     coordinated across every Fenrir.Application.Login.Services caller that logs to this category so two
///     unrelated events can never share the same (Category, EventCode) pair by accident within this project.
///     EventCode has no cross-project central catalog yet (see game.EventLog.sql's own "app-owned numbering
///     scheme, not FK'd to a lookup table" comment) -- this is the local one for Login.Services. A sibling
///     caller in a different project is not covered by this list and must coordinate separately until a
///     real cross-project registry exists.
/// </summary>
internal static class AccountSecurityEventCodes
{
    /// <summary>One rejected mouse-PIN verification attempt (op15) that did not yet cross the lockout threshold.</summary>
    public const short MousePinMismatch = 1;

    /// <summary>
    ///     The specific mouse-PIN attempt (op15) that crossed VerifyMousePinHandler.MaxPinFailures and disconnected the
    ///     session.
    /// </summary>
    public const short MousePinLockout = 2;

    /// <summary>A character was successfully renamed via op19 CL_CHANGE_AVATAR_NAME_SEND.</summary>
    public const short AvatarRenamed = 3;

    /// <summary>
    ///     One rejected mouse-PIN CHANGE attempt (op14) that did not yet cross the lockout threshold --
    ///     the op14 analog of <see cref="MousePinMismatch" />, added to close the audit-trail asymmetry
    ///     between the two opcodes sharing the same PinFailureCount counter (see
    ///     Fenrir.Application.Login.Services.ChangeMousePin.ChangeMousePinService's own remarks).
    /// </summary>
    public const short MousePinChangeMismatch = 4;

    /// <summary>
    ///     The specific mouse-PIN CHANGE attempt (op14) that crossed ChangeMousePinHandler.MaxPinFailures
    ///     and disconnected the session -- the op14 analog of <see cref="MousePinLockout" />.
    /// </summary>
    public const short MousePinChangeLockout = 5;

    /// <summary>
    ///     A ChangeMousePin/VerifyMousePin request (op14/15 -- op13 CreateMousePin never consults the
    ///     lockout, since there is no existing PIN to guess against) was rejected outright because the
    ///     account-scoped, cross-reconnect PIN lockout (auth.AccountPins.LockedUntilUtc, Migrations/
    ///     028_account_pin_lockout.sql) was already active -- no PasswordHasher.Verify ever ran for this
    ///     specific request. Shared by both opcodes rather than split like the mismatch/lockout pair above,
    ///     since this event describes the lockout mechanism itself rather than a specific guessing attempt.
    /// </summary>
    public const short MousePinAttemptRejectedLocked = 6;
}
