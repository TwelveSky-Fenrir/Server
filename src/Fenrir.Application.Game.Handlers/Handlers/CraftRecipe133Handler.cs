using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftRecipe133Handler(ILogger<CraftRecipe133Handler> logger)
    : IInlinePacketHandler<CraftRecipe133Request>
{
    public void Handle(in CraftRecipe133Request packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 133);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
