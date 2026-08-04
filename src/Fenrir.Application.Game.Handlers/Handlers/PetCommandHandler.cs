using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PetCommandHandler(ILogger<PetCommandHandler> logger)
    : IInlinePacketHandler<PetCommandRequest>
{
    public void Handle(in PetCommandRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 115);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
