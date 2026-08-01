using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class DuelAnswerHandler(IDuelService duelService, ILogger<DuelAnswerHandler>? logger = null)
    : IInlinePacketHandler<DuelAnswerRequest>
{
    public void Handle(in DuelAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        logger?.LogDebug("Duel answer received: session {SessionId} character {CharacterId} answer {Answer}",
            session.SessionId, targetId, packet.Answer);

        if (packet.Answer is not (0 or 1 or 2))
        {
            logger?.LogInformation(
                "Duel answer rejected: session {SessionId} character {CharacterId} sent out-of-range answer code {Answer}",
                session.SessionId, targetId, packet.Answer);
            return;
        }

        duelService.Answer(targetId, packet.Answer);
    }
}
