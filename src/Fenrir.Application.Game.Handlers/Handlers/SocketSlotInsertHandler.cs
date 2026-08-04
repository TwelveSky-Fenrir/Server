using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SocketSlotInsertHandler(ILogger<SocketSlotInsertHandler> logger)
    : IInlinePacketHandler<SocketSlotInsertRequest>
{
    public void Handle(in SocketSlotInsertRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 121);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
