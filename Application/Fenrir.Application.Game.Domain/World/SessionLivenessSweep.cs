using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Periodic background check: any GameServer connection currently registered in <see cref="SessionRegistry" />
///     that has produced no successfully-dispatched frame for <see cref="IdleTimeout" /> is forcibly disconnected
///     -- the Fenrir-side unification of legacy's three-branch per-tick user-liveness loop into one
///     state-agnostic rule (see this class's own remarks for why the three branches collapse cleanly).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1963-2006 ("User liveness/heartbeat loop", run once per
///     ~2.004 Hz <c>MyGame::Logic</c> tick over every user slot): an unauthenticated-but-connected user idle
///     three minutes or more is disconnected; an authenticated ("ready") user overdue for re-registration
///     (three minutes since its last registration, not mid-zone-transfer) whose heartbeat re-registration call
///     to the <c>playuser</c> process fails is disconnected; a connected user reaching neither the "ready" nor
///     recently-registered branches is disconnected after three minutes of raw connect time.
///     Fenrir has no separate <c>playuser</c> process to call for re-registration, so the faithfully-translated
///     BEHAVIOR (not the literal C++ call chain) is: any connected session -- pre-authentication or fully
///     in-world alike -- that stops producing successfully-dispatched traffic for three minutes is unilaterally
///     disconnected. All three legacy branches share the identical three-minute window in the cited range, so a
///     single <see cref="ClientSession.LastActivityUtc" />-based check (seeded at accept time, re-stamped on
///     every successfully-dispatched frame -- see that property's own remarks and
///     Network/Fenrir.Network.Dispatch/SessionLoop.cs) reproduces all three without needing to special-case
///     session state: a connection that never sends a single valid frame is caught exactly like one that goes
///     silent after being fully authenticated, and a connection that authenticates but then never re-sends
///     anything is caught the same way a stale heartbeat would have been.
///     Deliberately does NOT replace <see cref="ZoneWar.TempRegistrationIdleSweep" />: that sweep also frees a
///     <see cref="ZoneWar.TribeQuotaRegistry" /> slot the moment a TEMP_REGISTER_SEND (op11) registration goes
///     stale, which this generic sweep knows nothing about -- the two run independently and safely overlap
///     (<see cref="ClientSession.Abort" /> is idempotent) rather than one subsuming the other.
/// </remarks>
public sealed class SessionLivenessSweep(SessionRegistry registry, ILogger<SessionLivenessSweep> logger)
{
    /// <summary>Legacy's shared three-minute threshold for every branch of the cited user-liveness loop.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    ///     Disconnects every session <see cref="SessionRegistry.SnapshotIdle" /> reports as idle as of
    ///     <paramref name="nowUtc" />. No client-visible error packet is sent, matching the contract's
    ///     unilateral-disconnect failure semantics for this whole loop.
    /// </summary>
    public void Sweep(DateTimeOffset nowUtc)
    {
        foreach (var session in registry.SnapshotIdle(IdleTimeout, nowUtc))
        {
            logger.LogInformation(
                "Session liveness sweep: disconnecting session {SessionId} ({RemoteEndPoint}) -- idle since {LastActivityUtc:O}",
                session.SessionId, session.RemoteEndPoint, session.LastActivityUtc);

            session.Abort(DisconnectReason.IdleTimeout);
        }
    }
}
