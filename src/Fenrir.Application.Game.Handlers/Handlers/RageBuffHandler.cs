using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class RageBuffHandler(ILogger<RageBuffHandler> logger)
    : IInlinePacketHandler<RageBuffRequest>
{
    public void Handle(in RageBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 117);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
