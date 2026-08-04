using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftRecipe138Handler(ILogger<CraftRecipe138Handler> logger)
    : IInlinePacketHandler<CraftRecipe138Request>
{
    public void Handle(in CraftRecipe138Request packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 138);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
