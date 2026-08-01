using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FriendAnswerHandler(IFriendService friendService, ILogger<FriendAnswerHandler> logger)
    : IInlinePacketHandler<FriendAnswerRequest>
{
    public void Handle(in FriendAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("FriendAnswer: session {SessionId} character {CharacterId} answer {Answer}",
            session.SessionId, zoneSession.CharacterId, packet.Answer);

        if (packet.Answer is not (0 or 1 or 2))
            return;

        var targetId = zoneSession.CharacterId!.Value;

        friendService.Answer(targetId, packet.Answer);
    }
}
