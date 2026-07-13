using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FriendCancelHandler(IFriendService friendService, ILogger<FriendCancelHandler> logger)
    : IInlinePacketHandler<FriendCancelRequest>
{
    public void Handle(in FriendCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("FriendCancel: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        var askerId = zoneSession.CharacterId!.Value;

        friendService.Cancel(askerId);
    }
}
