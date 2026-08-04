using Fenrir.Application.Login.Sessions;
using Fenrir.Domain.Login;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login;

public sealed class LoginSessionLivenessSweep(
    LoginIdleClock idleClock,
    IOptions<LoginServerOptions> options,
    ILogger<LoginSessionLivenessSweep> logger)
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    public void Sweep(DateTimeOffset nowUtc)
    {
        var preAuthenticationTimeout = TimeSpan.FromSeconds(
            options.Value.PreAuthenticationHandshakeTimeoutSeconds);

        foreach (var (session, acceptedUtc) in idleClock.SnapshotPreAuthenticationExpired(
                     preAuthenticationTimeout, nowUtc))
        {
            if (!session.TryExpirePreAuthentication())
                continue;

            logger.LogInformation(
                "Login session handshake deadline: disconnecting unauthenticated session {SessionId} ({RemoteEndPoint}) -- accepted at {AcceptedUtc:O} without primary authentication",
                session.SessionId, session.RemoteEndPoint, acceptedUtc);
        }

        foreach (var (session, idleSinceUtc) in idleClock.SnapshotExpired(IdleTimeout, nowUtc))
        {
            if (session.IsPreAuthentication)
                continue;

            logger.LogInformation(
                "Login session liveness sweep: disconnecting session {SessionId} ({RemoteEndPoint}) -- no idle-clock opcode since {IdleSinceUtc:O}",
                session.SessionId, session.RemoteEndPoint, idleSinceUtc);

            session.Abort(DisconnectReason.IdleTimeout);
        }
    }
}
