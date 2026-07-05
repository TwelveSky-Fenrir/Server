using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_ASK_SEND (opcode 53) -- map 124 silently ignored (scripted-duel server). Tribe mismatch
///     always refuses; no inter-tribe exception here unlike duel/trade/party.
/// </summary>
public sealed class FriendAskHandler(FriendRegistry friends) : IInlinePacketHandler<FriendRequest>
{
    private const int MaxFriends = 10;

    public void Handle(in FriendRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone || zone.MapId == 124)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            session.Send(new FriendAnswerResponse { Answer = 4 });
            return;
        }

        if (asker.Friends.Count >= MaxFriends || asker.Friends.Values.Contains(target.CharacterId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (asker.Tribe != target.Tribe)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        switch (friends.TryAsk(askerId, target.CharacterId))
        {
            case FriendAskOutcome.AskerBusy:
                session.Send(new FriendAnswerResponse { Answer = 3 });
                return;
            case FriendAskOutcome.TargetBusy:
                session.Send(new FriendAnswerResponse { Answer = 5 });
                return;
            case FriendAskOutcome.Sent:
                target.Session.Send(new FriendResponse { AvatarName = asker.Name });
                return;
        }
    }
}
