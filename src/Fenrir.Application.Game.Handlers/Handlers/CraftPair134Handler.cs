using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftPair134Handler(ILogger<CraftPair134Handler> logger)
    : IInlinePacketHandler<CraftPair134Request>
{
    public void Handle(in CraftPair134Request packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 134);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
