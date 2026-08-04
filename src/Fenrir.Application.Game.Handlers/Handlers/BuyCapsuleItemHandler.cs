using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class BuyCapsuleItemHandler(ILogger<BuyCapsuleItemHandler> logger)
    : IInlinePacketHandler<BuyCapsuleItemRequest>
{
    public void Handle(in BuyCapsuleItemRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 114);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
