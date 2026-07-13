using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FriendAnswerHandler(IFriendService friendService, ILogger<FriendAnswerHandler> logger)
    : IInlinePacketHandler<FriendAnswerRequest>
{
    public void Handle(in FriendAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("FriendAnswer: session {SessionId} character {CharacterId} answer {Answer}",
            session.SessionId, zoneSession.CharacterId, packet.Answer);

        if (packet.Answer is not (0 or 1 or 2))
            return;

        var targetId = zoneSession.CharacterId!.Value;

        friendService.Answer(targetId, packet.Answer);
    }
}
