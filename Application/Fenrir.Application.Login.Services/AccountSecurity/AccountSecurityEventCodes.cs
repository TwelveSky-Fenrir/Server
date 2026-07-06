namespace Fenrir.Application.Login.Services.AccountSecurity;

/// <summary>
///     Locally-scoped game.EventLog.EventCode catalog for <see cref="EventLogCategory.AccountSecurity" />,
///     coordinated across every Fenrir.Application.Login.Services caller that logs to this category so two
///     unrelated events can never share the same (Category, EventCode) pair by accident within this project.
///     EventCode has no cross-project central catalog yet (see game.EventLog.sql's own "app-owned numbering
///     scheme, not FK'd to a lookup table" comment) -- this is the local one for Login.Services. A sibling
///     caller in a different project (e.g. a future ChangeMousePinService AccountSecurity row) is not
///     covered by this list and must coordinate separately until a real cross-project registry exists.
/// </summary>
internal static class AccountSecurityEventCodes
{
    /// <summary>One rejected mouse-PIN verification attempt (op15) that did not yet cross the lockout threshold.</summary>
    public const short MousePinMismatch = 1;

    /// <summary>The specific mouse-PIN attempt (op15) that crossed VerifyMousePinHandler.MaxPinFailures and disconnected the session.</summary>
    public const short MousePinLockout = 2;

    /// <summary>A character was successfully renamed via op19 CL_CHANGE_AVATAR_NAME_SEND.</summary>
    public const short AvatarRenamed = 3;
}
