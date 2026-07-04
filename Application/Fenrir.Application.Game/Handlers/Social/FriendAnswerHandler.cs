using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_ANSWER_SEND (opcode 55). 0 = accept, 1/2 = refuse, else ignored. On accept, BOTH sides become
///     eligible to call their own CZ_FRIEND_MAKE_SEND (<see cref="FriendRegistry.TryConsumeAccepted" />) -- neither side
///     is automatically added.
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
