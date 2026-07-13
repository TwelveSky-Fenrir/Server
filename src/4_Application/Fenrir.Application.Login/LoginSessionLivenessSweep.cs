using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login;

public sealed class LoginSessionLivenessSweep(SessionRegistry registry, ILogger<LoginSessionLivenessSweep> logger)
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

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
