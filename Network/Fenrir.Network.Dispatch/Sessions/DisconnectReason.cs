namespace Fenrir.Network.Dispatch.Sessions;

/// <summary>Why a session was torn down — exported as a metric tag, never exposed to the client.</summary>
public enum DisconnectReason
{
    ClientClosed,
    Malformed,
    UnknownOpcode,
    StateViolation,
    RateLimited,
    SlowConsumer,
    ServerShutdown,
    Evicted,
    Faulted,

    /// <summary>
    ///     Torn down because <see cref="FloodProtection.IpFloodGuard" /> just persisted a flood block for this
    ///     session's remote IP (raw-connection flood or protocol-violation flood; contract's Side effects §1
    ///     covers why the two are never distinguishable from the persisted row alone).
    /// </summary>
    IpBlocked,

    /// <summary>
    ///     Torn down because a GM-issued admin.Bans row was just created against this session's own account or
    ///     character (e.g. GM-BLOCK, legacy case 519). Distinct from <see cref="Faulted" /> so operators can tell
    ///     an administrative ban apart from a protocol/anti-tamper fault in the disconnect-reason metric.
    /// </summary>
    Banned,

    /// <summary>
    ///     Torn down by the Game-side TEMP_REGISTER_SEND (op11/ZoneHandshake) idle-timeout sweep: a connection
    ///     that completed the tribe-quota handshake but never followed up with avatar-selection/ready within
    ///     three minutes (Server/ts25zone/S07_MyGame01.cpp:1963-1990).
    /// </summary>
    IdleTimeout,

    /// <summary>
    ///     Torn down because an unhandled exception reached a request-processing failure boundary after that
    ///     request's own precondition/anti-tamper checks had already passed (e.g. EnterWorldService.HandleAsync's
    ///     CompleteWorldEntryAsync segment, from equipment/stat computation through posting the world-entry
    ///     command). Distinct from <see cref="Faulted" />, which covers an explicit, already-validated
    ///     precondition rejection (firewall/ban/ticket mismatch/dropped zone command/etc.) with no exception
    ///     involved -- this value exists purely so operators can tell "a real bug/transient failure fired mid-
    ///     processing" apart from "the request was rejected as designed" in the disconnect-reason metric. No
    ///     Server/ citation applies: this is a Fenrir-only call-chain exception-handling gap, not a legacy
    ///     behavior being mirrored.
    /// </summary>
    ProcessingFault
}
