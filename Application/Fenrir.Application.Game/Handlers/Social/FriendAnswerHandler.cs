using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_ANSWER_SEND (opcode 55) -- on accept, both sides become eligible to call their own
///     CZ_FRIEND_MAKE_SEND; neither is added automatically.
/// </summary>
public sealed class FriendAnswerHandler(ZoneRegistry zones, FriendRegistry friends)
    : IInlinePacketHandler<FriendAnswerRequest>
{
    public void Handle(in FriendAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        if (!friends.TryAnswer(targetId, packet.Answer == 0, out var askerId))
            return;

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new FriendAnswerResponse { Answer = packet.Answer });
    }
}
