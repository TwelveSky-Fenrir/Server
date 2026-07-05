using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_FRIEND_CANCEL_SEND (opcode 54) -- withdraws the caller's own still-pending ask.</summary>
public sealed class FriendCancelHandler(IFriendService friendService) : IInlinePacketHandler<FriendCancelRequest>
{
    public void Handle(in FriendCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        friendService.Cancel(askerId);
    }
}
