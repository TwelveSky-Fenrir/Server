namespace Fenrir.Data.Abstractions.Security;

/// <summary>
///     Legacy <c>macinfo</c> (<c>MyDB::Login</c>, <c>Server/ts25login/S08_MyDB.cpp:431-479</c>). Only the
///     "PC has been banned" half is ported (<c>AccountLimit &lt;= 0</c>): the legacy concurrent-session cap
///     (how many *currently logged-in* accounts share this MAC/install right now) needs a live directory of
///     active sessions keyed by MAC, which Fenrir doesn't maintain -- a real gap, deliberately not invented
///     here as a side effect of this cluster.
/// </summary>
public interface IMacRestrictionRepository
{
    /// <summary>
    ///     A MachineGuid match alone is enough to ban, independent of MacAddress -- mirrors legacy's ban lookup,
    ///     which is keyed solely by the client-declared adapter GUID string (Server/ts25login/S08_MyDB.cpp:441).
    ///     The MAC-wide default row (MachineGuid IS NULL, admin.MacRestrictions) is a Fenrir-only elaboration
    ///     with no legacy analog. See <c>MacRestrictionRepository.SelectRestriction</c>'s own remarks for the
    ///     full precedence rules.
    /// </summary>
    public ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct);
}
