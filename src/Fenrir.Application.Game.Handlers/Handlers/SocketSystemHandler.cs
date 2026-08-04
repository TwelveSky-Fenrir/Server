using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SocketSystemHandler(ILogger<SocketSystemHandler> logger)
    : IInlinePacketHandler<SocketSystemRequest>
{
    public void Handle(in SocketSystemRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 98);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
