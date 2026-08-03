using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class LoginEventHandler(ILogger<LoginEventHandler> logger)
    : IInlinePacketHandler<LoginEventRequest>
{
    public void Handle(in LoginEventRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: LoginEventRequest received — op101 P_LOGIN_EVENT_SEND1 has no live REGWORK1 line " +
            "in legacy MyWork::Init: the only one is commented out and names LOGIN_EVENT_SEND, a symbol that does " +
            "not exist (Server/ts25zone/S04_MyWork01.cpp:109), so the legacy server logs Unknown Header and quits " +
            "the session (:292-301); silently ignored",
            session.SessionId);
    }
}
