using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class SessionLivenessSweep(SessionRegistry registry, ILogger<SessionLivenessSweep> logger)
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(3);

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
