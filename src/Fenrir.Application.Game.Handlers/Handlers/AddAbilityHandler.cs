using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class AddAbilityHandler(ILogger<AddAbilityHandler> logger)
    : IInlinePacketHandler<AddAbilityRequest>
{
    public void Handle(in AddAbilityRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 132);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
