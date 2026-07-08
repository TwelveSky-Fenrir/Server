using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op12 CL_CLIENT_OK_FOR_LOGIN_SEND — only promotes Authenticated to CharSelect; must not bypass the PinRequired
///     gate (only ops 13/14/15 may open it).
/// </summary>
public sealed class LoginKeepAliveHandler(ILogger<LoginKeepAliveHandler> logger)
    : IInlinePacketHandler<LoginKeepAliveRequest>
{
    public void Handle(in LoginKeepAliveRequest packet, IPacketSession session)
    {
        var loginSession = (LoginClientSession)session;
        if (loginSession.State == LoginSessionState.Authenticated)
        {
            loginSession.MarkCharSelect();
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Session {SessionId}: op12 CL_CLIENT_OK_FOR_LOGIN_SEND -- Authenticated -> CharSelect",
                    session.SessionId);
        }
        else if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Session {SessionId}: op12 CL_CLIENT_OK_FOR_LOGIN_SEND ignored -- session already in state {State}",
                session.SessionId, loginSession.State);
        }
    }
}
