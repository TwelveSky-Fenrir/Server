namespace Fenrir.Application.Login;

/// <summary>
///     Bound from the <c>Login</c> configuration section. Defaults match the legacy
///     <c>Server/BuildEU33/ServerInfo.ini</c> <c>[Login.Server]</c>/<c>[Server.Info]</c> values — the real client
///     (BuildEU33) will reject anything else, so these are the values that make the M1 acceptance test
///     (Docs/Fenrir_Architecture_Reference_2026.md, "vérification de bout en bout") pass, not arbitrary picks.
/// </summary>
public sealed class LoginServerOptions
{
    /// <summary><c>[Login.Server].Port</c> in the legacy ini.</summary>
    public int Port { get; init; } = 29998;

    /// <summary>
    ///     Compared against <c>ClLoginSend.Version</c> (wire contract §4.2: mismatch → <c>LcLoginRecv.Result = 4</c>).
    ///     <c>[Server.Info].Version</c> in the legacy ini — NOT the "33" used as a placeholder in the wire
    ///     contract's illustrative golden vector (§9.1).
    /// </summary>
    public int ExpectedClientVersion { get; init; } = 90354;

    /// <summary>Session ticket lifetime for the login→zone handover (ADR-0005: "TTL court").</summary>
    public int TicketTtlSeconds { get; init; } = 15;

    /// <summary>
    ///     <c>[Server.Info].MaxUser</c> in the legacy ini — reported to the client as <c>tMaxPlayerNum</c> in the op 0
    ///     greeting, informational only (not enforced as a hard cap in M1).
    /// </summary>
    public int MaxPlayerNum { get; init; } = 1000;

    /// <summary>
    ///     <c>[Login.Server].P2ndPassword</c> in the legacy ini — <c>=1</c> in prod EU33 (BuildEU33/ServerInfo.ini
    ///     l.127), which makes the mouse PIN MANDATORY: LC_LOGIN_RECV carries <c>tSecondLoginSort=1</c> and the
    ///     session parks in <c>PinRequired</c> until op 13 (create) or 15 (verify) opens the gate. Default matches
    ///     prod; turning it off reproduces <c>m2ndPassword=0</c> (session goes straight to character select).
    /// </summary>
    public bool RequireSecondPassword { get; init; } = true;
}
