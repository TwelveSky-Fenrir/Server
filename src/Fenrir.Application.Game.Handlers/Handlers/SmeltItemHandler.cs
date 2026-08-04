using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SmeltItemHandler(ILogger<SmeltItemHandler> logger)
    : IInlinePacketHandler<SmeltItemRequest>
{
    public void Handle(in SmeltItemRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 102);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
