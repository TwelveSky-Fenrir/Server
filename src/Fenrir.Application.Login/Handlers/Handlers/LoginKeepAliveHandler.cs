using Fenrir.Application.Login.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

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
