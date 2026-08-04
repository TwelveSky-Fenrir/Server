using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ServiceMoveHandler(ILogger<ServiceMoveHandler> logger)
    : IInlinePacketHandler<ServiceMoveRequest>
{
    public void Handle(in ServiceMoveRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 116);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
