using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftRecipe135Handler(ILogger<CraftRecipe135Handler> logger)
    : IInlinePacketHandler<CraftRecipe135Request>
{
    public void Handle(in CraftRecipe135Request packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 135);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
