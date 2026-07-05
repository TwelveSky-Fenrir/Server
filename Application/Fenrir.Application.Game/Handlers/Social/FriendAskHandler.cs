using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_ASK_SEND (opcode 53) -- map 124 silently ignored (scripted-duel server). Tribe mismatch
///     always refuses; no inter-tribe exception here unlike duel/trade/party.
/// </summary>
public sealed class FriendAskHandler(IFriendService friendService) : IInlinePacketHandler<FriendRequest>
{
    public void Handle(in FriendRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        switch (friendService.Ask(zone, asker, packet.AvatarName))
        {
            case FriendAskResultKind.MapForbidden:
                return;
            case FriendAskResultKind.TargetNotFound:
                session.Send(new FriendAnswerResponse { Answer = 4 });
                return;
            case FriendAskResultKind.AlreadyFriendOrFull:
            case FriendAskResultKind.TribeMismatch:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case FriendAskResultKind.AskerBusy:
                session.Send(new FriendAnswerResponse { Answer = 3 });
                return;
            case FriendAskResultKind.TargetBusy:
                session.Send(new FriendAnswerResponse { Answer = 5 });
                return;
        }
    }
}
