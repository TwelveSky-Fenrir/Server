namespace Fenrir.Application.Login.Domain;

/// <summary>
///     Bound from the <c>Login</c> config section; defaults match legacy <c>BuildEU33/ServerInfo.ini</c>, which the
///     real client requires.
/// </summary>
public sealed class LoginServerOptions
{
    public int Port { get; set; } = 29998;

    /// <summary>
    ///     Must match <c>LoginRequest.Version</c> or login fails with Result=4; legacy ini <c>[Server.Info].Version</c>
    ///     (not the "33" placeholder from wire contract §9.1).
    /// </summary>
    public int ExpectedClientVersion { get; set; } = 90354;

    public int TicketTtlSeconds { get; set; } = 15;

    /// <summary>Reported to client as tMaxPlayerNum; informational only, not enforced as a hard cap in M1.</summary>
    public int MaxPlayerNum { get; set; } = 1000;

    /// <summary>Legacy <c>P2ndPassword</c> (=1 in prod EU33): when true, mouse PIN is mandatory before character select.</summary>
    public bool RequireSecondPassword { get; set; } = true;

    /// <summary>
    ///     How often <c>AccountSessionLivenessHost</c> refreshes <c>runtime.AccountSessions.LastRefreshedUtc</c> for
    ///     every account this process holds a live Login session for -- keeps a long-lived Login session (still
    ///     authenticating, still at char-select) from being wrongly reaped by the 6-minute staleness sweep.
    /// </summary>
    public int AccountSessionRefreshIntervalSeconds { get; set; } = 60;

    /// <summary>
    ///     Trigger A of <c>Fenrir.Network.Dispatch.FloodProtection.IpFloodGuard</c>: max concurrent connections
    ///     from one IP to this process before it's persistently blocked (<c>admin.FirewallRules</c>). Legacy
    ///     hardcodes this to 40 as a compile-time <c>#define</c> (<c>MAX_CONNECTIONS_PER_IP</c>,
    ///     <c>Server/Header/enter_count.cpp:5</c>); Fenrir makes it operator-configurable instead, per that
    ///     feature's own explicit "strict improvement" mandate.
    /// </summary>
    public int MaxConnectionsPerIp { get; set; } = 40;

    /// <summary>
    ///     Trigger B of <c>Fenrir.Network.Dispatch.FloodProtection.IpFloodGuard</c>: max unrecognized-opcode
    ///     protocol violations from one IP within a rolling wall-clock hour before it's persistently blocked.
    ///     Legacy hardcodes this to 30 (<c>MAX_ERROR_PACKET_PER_IP</c>, <c>Server/Header/enter_count.cpp:6</c>),
    ///     but legacy's own escalation function has been dead code (no live call site) since before the current
    ///     unknown-opcode handling was written -- see <see cref="MaxConnectionsPerIp" />'s sibling type's own
    ///     remarks. This threshold is therefore NEW behavior beyond legacy parity, not a reproduction of a
    ///     currently-live legacy limit.
    /// </summary>
    public int MaxProtocolViolationsPerIpPerHour { get; set; } = 30;

    /// <summary>
    ///     op17 CL_CREATE_AVATAR_SEND2's fourth-faction (Tribe value 3) creation exclusion toggle -- when false
    ///     (the default), a create-avatar request for Tribe value 3 is rejected; see
    ///     <see cref="Fenrir.Application.Login.Domain.Avatars.FourthFactionGate" /> for the gate itself. Default
    ///     is false because that is legacy's own unconditional shipped behavior, not a conservative Fenrir
    ///     choice: the exclusion is compiled under the LNW33 build macro, and LNW33 is unconditionally active
    ///     (together with M33 and LNW33EU) in ReleaseEU33, the only configuration actually wired into the
    ///     committed solution and shipped -- there is no real, buildable legacy configuration where tribe 3 is
    ///     permitted at creation.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:640-646 (the exclusion itself) ;
    ///     ServerDocs/03_BUILD_VARIANTS_ET_PREPROCESSEUR.md:56-59 (LNW33 always active in ReleaseEU33).
    /// </remarks>
    public bool EnableFourthFaction { get; set; }
}
