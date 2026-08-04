using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class FourGuildChallengeDecisionHandler(ILogger<FourGuildChallengeDecisionHandler> logger)
    : IInlinePacketHandler<FourGuildChallengeDecisionRequest>
{
    public void Handle(in FourGuildChallengeDecisionRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 107);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
