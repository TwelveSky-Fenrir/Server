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

    /// <summary>
    ///     Max time <c>Fenrir.Application.Login.Services.ZoneTransfer.TcpShardReachabilityProbe</c> waits for a
    ///     TCP connect to succeed against a candidate shard's Host:Port before
    ///     <c>Fenrir.Application.Login.Services.ZoneTransfer.ZoneTransferService</c> gives up on it and reports
    ///     <c>ShardUnavailable</c> instead of minting a ticket that would hand the client a dead address. The
    ///     shard directory alone only proves a recent heartbeat (up to ~17s stale, accounting for the SQL
    ///     freshness window plus the directory repository's own read cache) -- not that the shard is still
    ///     alive right now.
    /// </summary>
    public int ShardReachabilityProbeTimeoutMilliseconds { get; set; } = 750;

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
    ///     How often <c>LoginSessionLivenessSweepHost</c> polls every currently-registered Login connection for
    ///     <c>LoginSessionLivenessSweep.IdleTimeout</c>'s 60-second no-successfully-dispatched-frame window
    ///     (Server/ts25login/S07_MyGame01.cpp:37-73). A poll cadence, not the timeout itself -- a real
    ///     forced-disconnect never lags the 60-second cutoff by more than one cycle. Deliberately a much
    ///     tighter default than GameServer's equivalent (<c>GameServerOptions.SessionLivenessSweepIntervalSeconds</c>,
    ///     30s) because Login's own idle threshold is a minute, not three -- a 30s cadence against a 60s threshold
    ///     would let a forced-disconnect lag by up to half the threshold itself.
    /// </summary>
    public int IdleSweepIntervalSeconds { get; set; } = 1;

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

    /// <summary>
    ///     Legacy <c>mSERVER_INFO.mOnlyAdmin</c> operator lockdown flag: when true, a non-GM-tier account
    ///     (<c>AccountGrade &lt; 1</c>) is rejected at login with the fixed application message "Only Admin
    ///     can login", while GM-tier accounts still log in normally. Intended for maintenance windows where
    ///     only staff should be able to connect. Whether this flag was ever enabled in the shipped EU33
    ///     production configuration was not confirmed when this option was added -- defaults to
    ///     <see langword="false" /> (disabled) so it changes nothing unless an operator explicitly opts in.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25login/S04_MyWork02.cpp:202-208.</remarks>
    public bool OnlyAdminCanLogin { get; set; }
}
