namespace Fenrir.Application.Login.Domain.Security;

/// <summary>
///     Legacy "Protect Spoofed" anti-spoofing gate (<c>MyDB::Login</c>): for a non-GM-tier account, rejects a
///     login whose declared MAC address, declared adapter name/GUID, or server-observed remote IP is an exact,
///     case-sensitive match to one of three fixed placeholder literals. GM-tier accounts (grade one or above)
///     are exempt entirely.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S08_MyDB.cpp:497-507 (the grade-below-one guard and the three-way exact-text
///     comparison against <c>"00-00-00-00-00-00"</c>, <c>"{0-0-0-0-0}"</c>, and <c>"127.0.0.1"</c>) ;
///     Server/ts25login/S08_MyDB.cpp:427-430 (the four placeholder-default values) ;
///     Server/ts25login/S08_MyDB.cpp:431-486 (the abuse-limit block: gated on a non-empty declared MAC, only
///     when it runs are the real client/connection values substituted for the placeholders -- a zero-length
///     declared MAC skips it entirely, leaving all three values this gate inspects at their placeholder
///     defaults) ; Server/ts25login/S08_MyDB.cpp:244-245 (account grade read directly off the account row,
///     confirming it is durable per-account data, not derived) ; Server/Header/safestring.h:33-36 (the
///     placeholder comparisons are exact, case-sensitive string equality, not a byte-level zero test).
/// </remarks>
/// <remarks>
///     Decoupling note: the durable account-grade column this gate depends on
///     (<c>auth.Accounts.AccountGrade</c>, <see cref="Fenrir.Data.Abstractions.Accounts.AuthenticateAccountDto" />
///     ) was added by <c>Database/Migrations/011_accounts_add_account_grade.sql</c> to close the same
///     "auth.Accounts has no account-grade/GM flag yet" gap a concurrent Game-side feature (GM-BLOCK,
///     legacy case 519) needed first. This gate only reads that column through the Login-side DTO already
///     produced by <c>usp_Account_Authenticate</c> -- it has no dependency on, and never touches, the
///     Game-side session-ticket hand-off (<c>runtime.SessionTickets.AccountGrade</c>,
///     <c>Database/Migrations/012_session_tickets_add_account_grade.sql</c>) that feature separately wires up.
/// </remarks>
public static class DeviceSpoofingGuard
{
    private const string PlaceholderMacAddress = "00-00-00-00-00-00";
    private const string PlaceholderAdapterGuid = "{0-0-0-0-0}";
    private const string PlaceholderRemoteIp = "127.0.0.1";

    /// <summary>
    ///     Grade values at or above this threshold are GM-tier and exempt from this gate. Also reused by
    ///     <c>Fenrir.Application.Login.Services.Login.LoginService</c>'s own GM-IP allowlist gate (the "# GM
    ///     Enable Login IP #" check, Server/ts25login/S04_MyWork02.cpp:192-201) so both AccountGrade-gated
    ///     post-authentication checks share the exact same elevation threshold.
    /// </summary>
    public const int GmGradeThreshold = 1;

    /// <summary>
    ///     Evaluates the gate. <paramref name="declaredMacAddress" /> is the already-formatted
    ///     "AA-BB-CC-..." text produced from the client's raw adapter bytes (empty when the client declared
    ///     zero MAC length); <paramref name="declaredAdapterGuid" /> is the client's raw adapter name/GUID
    ///     field; <paramref name="observedRemoteIp" /> is the login server's own observed address for the
    ///     accepted connection, never client-supplied.
    /// </summary>
    public static bool IsSpoofedDeviceTuple(int accountGrade, string declaredMacAddress,
        string declaredAdapterGuid, string? observedRemoteIp)
    {
        if (accountGrade >= GmGradeThreshold)
            return false;

        // A zero-length declared MAC address skips the entire real-value population block in the legacy
        // routine (S08_MyDB.cpp:431-486), so the values it would otherwise compare here never leave their
        // placeholder defaults -- modeled directly rather than comparing whatever GUID/IP the client happened
        // to declare, since the legacy code never even reads them in this case.
        if (declaredMacAddress.Length == 0)
            return true;

        return declaredMacAddress == PlaceholderMacAddress
               || declaredAdapterGuid == PlaceholderAdapterGuid
               || observedRemoteIp == PlaceholderRemoteIp;
    }
}
