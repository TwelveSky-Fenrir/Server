using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_ASK_SEND (opcode 53, contracts/05_social.md). Map 124: silently ignored entirely (no
///     reply at all -- the scripted-duel server carve-out). Full list (MAX_FRIEND_NUM=10) or already a
///     friend ⇒ Quit(); tribe mismatch ⇒ Quit() (no alliance branch documented for this one, unlike
///     duel/trade/party). Target resolved WITHIN THE ASKER'S OWN ZONE ONLY (<c>SearchAvatar</c> scope).
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
