using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Domain;

/// <summary>
///     Periodic background check: any LoginServer connection currently registered in <see cref="SessionRegistry" />
///     that has produced no successfully-dispatched frame for <see cref="IdleTimeout" /> is forcibly disconnected --
///     pre-authentication or post-authentication alike, the same "one state-agnostic rule" posture as GameServer's
///     own <c>Fenrir.Application.Game.Domain.World.SessionLivenessSweep</c>, which this type otherwise mirrors
///     exactly (only the module and threshold differ).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S07_MyGame01.cpp:37-73 (MyGame::Logic's per-connection liveness loop): a
///     connection idle 60 seconds or more -- whether it never authenticated at all, or authenticated and then went
///     silent -- is disconnected. Fenrir has no separate per-branch state machine here either (see
///     <c>Fenrir.Application.Game.Domain.World.SessionLivenessSweep</c>'s own remarks for the identical reasoning
///     on the GameServer side): a single <see cref="ClientSession.LastActivityUtc" />-based check (seeded at
///     accept time, re-stamped on every successfully-dispatched frame -- see that property's own remarks and
///     Network/Fenrir.Network.Dispatch/SessionLoop.cs) reproduces the legacy behavior without needing to
///     special-case session state.
/// </remarks>
public sealed class LoginSessionLivenessSweep(SessionRegistry registry, ILogger<LoginSessionLivenessSweep> logger)
{
    /// <summary>Legacy's 60-second idle threshold (Server/ts25login/S07_MyGame01.cpp:37-73).</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

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
                "Login session liveness sweep: disconnecting session {SessionId} ({RemoteEndPoint}) -- idle since {LastActivityUtc:O}",
                session.SessionId, session.RemoteEndPoint, session.LastActivityUtc);

            session.Abort(DisconnectReason.IdleTimeout);
        }
    }
}
