using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class PartyAnswerHandler(
    ZoneRegistry zones,
    IPartyAnswerService partyAnswerService,
    ILogger<PartyAnswerHandler> logger) : IInlinePacketHandler<PartyAnswerRequest>
{
    public void Handle(in PartyAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("PartyAnswer: session {SessionId} character {CharacterId} answer {Answer}",
            session.SessionId, zoneSession.CharacterId, packet.Answer);

        var inviteeId = zoneSession.CharacterId!.Value;

        var result = partyAnswerService.Answer(inviteeId, packet.Answer);
        if (result.Kind == PartyAnswerResultKind.NotFound)
            return;

        if (zones.TryGetPlayer(result.InviterId, out var inviter))
            inviter.Session.Send(new PartyAnswerResponse { Answer = packet.Answer });

        if (!result.Accepted)
            return;

        if (result.JoinOutcome == PartyJoinOutcome.PartyWasFull)
        {
            var fullRoster = PartyBroadcast.BuildRoster(zones, 2, result.Members);
            foreach (var memberId in result.Members)
                if (zones.TryGetPlayer(memberId, out var member))
                    member.Session.Send(fullRoster);

            return;
        }

        if (!zones.TryGetPlayer(inviteeId, out var invitee))
            return;

        var joinNotice = new PartyMemberJoinedResponse { AvatarName = invitee.Name };
        var roster = PartyBroadcast.BuildRoster(zones, result.JoinOutcome == PartyJoinOutcome.Created ? 1 : 2,
            result.Members);

        foreach (var memberId in result.Members)
            if (zones.TryGetPlayer(memberId, out var member))
            {
                member.Session.Send(joinNotice);
                member.Session.Send(roster);
            }
    }
}
