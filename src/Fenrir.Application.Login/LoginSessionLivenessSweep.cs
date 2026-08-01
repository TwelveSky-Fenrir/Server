using Fenrir.Application.Login.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login;

public sealed class LoginSessionLivenessSweep(LoginIdleClock idleClock, ILogger<LoginSessionLivenessSweep> logger)
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    public void Sweep(DateTimeOffset nowUtc)
    {
        foreach (var (session, idleSinceUtc) in idleClock.SnapshotExpired(IdleTimeout, nowUtc))
        {
            logger.LogInformation(
                "Login session liveness sweep: disconnecting session {SessionId} ({RemoteEndPoint}) -- no idle-clock opcode since {IdleSinceUtc:O}",
                session.SessionId, session.RemoteEndPoint, idleSinceUtc);

            session.Abort(DisconnectReason.IdleTimeout);
        }
    }
}
