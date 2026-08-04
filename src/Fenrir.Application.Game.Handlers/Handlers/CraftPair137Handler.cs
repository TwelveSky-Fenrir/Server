using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftPair137Handler(ILogger<CraftPair137Handler> logger)
    : IInlinePacketHandler<CraftPair137Request>
{
    public void Handle(in CraftPair137Request packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 137);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
