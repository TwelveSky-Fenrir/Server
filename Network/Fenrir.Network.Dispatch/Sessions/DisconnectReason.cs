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
    TimedZoneExpired,

    /// <summary>
    ///     Torn down by an Admin-tier (<c>GmCommandTier.Admin</c>) GM data-command (opcode 19,
    ///     CZ_PROCESS_DATA_SEND): the MAX stat-cheat (tSort 509), and the shared privilege-gate-failure path
    ///     for that command plus its two siblings in the same tier (spawn-item tSort 505/523, grant-pet-
    ///     experience tSort 700). Legacy calls the exact same full-logout routine
    ///     (Server/ts25zone/S03_MyUser.cpp:338-410, resolved via the disconnect macro at
    ///     Server/Header/Protocol/DEFINE.h:651) on both the privilege-gate-failure branch AND the
    ///     tSort-509-success branch of Server/ts25zone/S04_MyWork04.cpp:1188-1202 -- distinct from
    ///     <see cref="Faulted" /> so operators can tell a deliberate, successful Admin-tier GM command's own
    ///     forced disconnect (which, for tSort 509, is what actually makes the stat mutation durable -- see
    ///     that command's own remarks) apart from an unauthorized-caller rejection or an unrelated fault, the
    ///     same distinction <see cref="Banned" /> already draws for GM-BLOCK.
    ///     <para>
    ///         Also used by the Basic-tier (<c>GmCommandTier.Basic</c>) TRIBE self-command's (tSort 510) own
    ///         successful-completion forced disconnect (Server/ts25zone/S04_MyWork04.cpp:1203-1264) -- the
    ///         identical "a deliberate, successful GM command's own forced disconnect IS the completion
    ///         signal" semantic as tSort 509 above, just at a lower tier; kept as one value rather than a
    ///         per-tier duplicate, since the metric distinction this value exists for (deliberate-success vs.
    ///         rejection/fault) does not depend on which tier the command gates at.
    ///     </para>
    /// </summary>
    GmCommandLogout,

    /// <summary>
    ///     Torn down because the Basic-tier (<c>GmCommandTier.Basic</c>) KICK GM command (CZ_PROCESS_DATA_SEND
    ///     tSort 518, Server/ts25zone/S04_MyWork04.cpp:1469-1486) just disconnected this session as its
    ///     TARGET -- distinct from <see cref="Banned" /> (which also creates a durable admin.Bans row) and
    ///     <see cref="Faulted" /> (an unrelated protocol/anti-tamper fault), so operators can tell a GM's
    ///     one-off disconnect-only action apart from either.
    /// </summary>
    GmKicked,

    /// <summary>
    ///     Torn down by the Valley of the Deceased (Zone 200/297/298/299 gate/door/kill-race "Regular War")
    ///     forced-reset step: every session on the map is disconnected unconditionally once the post-conclusion
    ///     cooldown elapses, immediately before the whole cycle resets to its idle wait -- see
    ///     <c>Fenrir.Application.Game.Domain.World.ZoneWar.ValleyWarSystem</c> for the one caller. Distinct from
    ///     <see cref="TimedZoneExpired" /> (a per-player countdown) since this fires identically for every
    ///     connected session on the map regardless of that player's own state.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11558-11571.</remarks>
    ValleyWarForcedReset,

    /// <summary>
    ///     Torn down by op23's costume/stellar-core intermediate dispatch (workstream C9-costume-stellar-
    ///     whitelist) when every one of the ten wardrobe slots is already occupied at the moment a new
    ///     costume/stellar-core item is used -- a real, distinct legacy <c>Quit()</c> condition, not a
    ///     protocol/anti-tamper fault (<see cref="Faulted" />) or an anti-exploit state re-check
    ///     (<see cref="StateViolation" />). No acknowledgment of any kind is sent first, matching the legacy
    ///     source's own asymmetry against the sibling "already worn" rejection (a normal Result=1 reply, no
    ///     disconnect).
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2503-2514 (costume), :2542-2553 (stellar core).</remarks>
    WardrobeFull,

    /// <summary>
    ///     Torn down by the Zone175 "Labyrinth" 5-wave PvE mission's own terminal-state kick: every session still
    ///     on the map is force-disconnected once the mission concludes (wave 5 cleared, or an empty-zone abort)
    ///     and its post-conclusion hold elapses -- see
    ///     <c>Fenrir.Application.Game.Domain.World.Zone.ForceDisconnectAllForZone175</c> for the one caller.
    ///     Distinct from <see cref="TimedZoneExpired" />, which is the unrelated Hoisundo rebirth-event zones'
    ///     own per-player forced-departure countdown, not this mission's all-sessions terminal kick.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:8609-9311.</remarks>
    LabyrinthMissionEnded
}
