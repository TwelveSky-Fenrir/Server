using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_ANSWER_SEND (opcode 55) -- on accept, both sides become eligible to call their own
///     CZ_FRIEND_MAKE_SEND; neither is added automatically.
/// </summary>
public sealed class FriendAnswerHandler(IFriendService friendService) : IInlinePacketHandler<FriendAnswerRequest>
{
    public void Handle(in FriendAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        friendService.Answer(targetId, packet.Answer);
    }
}
