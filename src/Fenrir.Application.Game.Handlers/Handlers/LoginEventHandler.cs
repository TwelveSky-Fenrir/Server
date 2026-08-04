using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class LoginEventHandler(ILogger<LoginEventHandler> logger)
    : IInlinePacketHandler<LoginEventRequest>
{
    public void Handle(in LoginEventRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 101);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
