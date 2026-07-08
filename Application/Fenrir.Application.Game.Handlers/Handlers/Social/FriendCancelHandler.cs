using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_FRIEND_CANCEL_SEND (opcode 54) -- withdraws the caller's own still-pending ask.</summary>
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
