using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

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
