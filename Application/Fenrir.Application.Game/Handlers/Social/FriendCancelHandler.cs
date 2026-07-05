using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_FRIEND_CANCEL_SEND (opcode 54) -- withdraws the caller's own still-pending ask.</summary>
public sealed class FriendCancelHandler(ZoneRegistry zones, FriendRegistry friends)
    : IInlinePacketHandler<FriendCancelRequest>
{
    public void Handle(in FriendCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        if (!friends.TryCancel(askerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new FriendCancelResponse());
    }
}
