using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class TrapCheckHandler(ILogger<TrapCheckHandler> logger)
    : IInlinePacketHandler<TrapCheckRequest>
{
    public void Handle(in TrapCheckRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 106);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
