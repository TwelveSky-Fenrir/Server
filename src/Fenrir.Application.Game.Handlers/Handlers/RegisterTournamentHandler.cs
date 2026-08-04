using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class RegisterTournamentHandler(ILogger<RegisterTournamentHandler> logger)
    : IInlinePacketHandler<RegisterTournamentRequest>
{
    public void Handle(in RegisterTournamentRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 150);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
