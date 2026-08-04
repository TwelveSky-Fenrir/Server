using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class DiceBattleHandler(ILogger<DiceBattleHandler> logger)
    : IInlinePacketHandler<DiceBattleRequest>
{
    public void Handle(in DiceBattleRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 96);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
