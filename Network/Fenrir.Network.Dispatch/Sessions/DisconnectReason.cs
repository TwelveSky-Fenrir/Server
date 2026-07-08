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
    ///     Torn down by one of several idle-connection sweeps across both servers, each mirroring its own
    ///     process's per-tick/per-loop user-liveness check:
    ///     <list type="bullet">
    ///         <item>
    ///             GameServer, mirroring Server/ts25zone/S07_MyGame01.cpp:1963-2006: either the narrower
    ///             TEMP_REGISTER_SEND (op11/ZoneHandshake) sweep -- a connection that completed the tribe-quota
    ///             handshake but never followed up with avatar-selection/ready within three minutes -- or the
    ///             general connection-liveness sweep covering every other case (never authenticated at all, or
    ///             authenticated and then went silent) using the same three-minute threshold. See
    ///             <c>TempRegistrationIdleSweep</c> and <c>Fenrir.Application.Game.Domain.World.SessionLivenessSweep</c>
    ///             for the two consumers.
    ///         </item>
    ///         <item>
    ///             LoginServer, mirroring Server/ts25login/S07_MyGame01.cpp:37-73: a connection idle 60+ seconds
    ///             -- pre-authentication or post-authentication alike -- is disconnected. See
    ///             <c>Fenrir.Application.Login.Domain.LoginSessionLivenessSweep</c> for the one consumer.
    ///         </item>
    ///     </list>
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
    ProcessingFault,

    /// <summary>
    ///     Torn down by the "Hoisundo" forced-departure countdown for the rebirth-event zones 234-240
    ///     (Server/ts25zone/S07_MyGame01.cpp:1748-1865, <c>user-&gt;Quit()</c> once the per-zone countdown
    ///     drops below 1). See <c>Fenrir.Application.Game.Domain.Simulation.HoisundoCountdownSystem</c> for the
    ///     one consumer.
    /// </summary>
    TimedZoneExpired
}
