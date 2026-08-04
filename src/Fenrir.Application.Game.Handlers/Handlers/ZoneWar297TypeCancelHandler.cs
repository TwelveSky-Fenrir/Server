using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ZoneWar297TypeCancelHandler(ILogger<ZoneWar297TypeCancelHandler> logger)
    : IInlinePacketHandler<ZoneWar297TypeCancelRequest>
{
    public void Handle(in ZoneWar297TypeCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 100);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
