using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_ANSWER_SEND (opcode 67). 0 = accept, 1/2 = refuse, else ignored (legacy's own
///     <c>default: return</c>). On accept, echoes ZC 73 to the inviter, then broadcasts
///     ZC_PARTY_JOIN_INFO (75) + a full roster refresh (74) to every member -- unless the party was
///     already full (<see cref="PartyJoinOutcome.PartyWasFull" />, silent legacy no-op; see
///     <see cref="PartyRegistry" />). Collapses ts25center's separate PARTY_JOIN/PARTY_INFO emissions
///     into one fan-out.
/// </summary>
public sealed class PartyAnswerHandler(ZoneRegistry zones, PartyRegistry parties)
    : IInlinePacketHandler<PartyAnswerRequest>
{
    public void Handle(in PartyAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var inviteeId = zoneSession.CharacterId!.Value;

        var accepted = packet.Answer == 0;
        if (!parties.TryAnswer(inviteeId, accepted, out var inviterId, out var joinOutcome))
            return;

        if (zones.TryGetPlayer(inviterId, out var inviter))
            inviter.Session.Send(new PartyAnswerResponse { Answer = packet.Answer });

        if (!accepted || joinOutcome == PartyJoinOutcome.PartyWasFull)
            return;

        if (!zones.TryGetPlayer(inviteeId, out var invitee))
            return;

        var members = parties.GetMembers(inviterId);

        var joinNotice = new PartyMemberJoinedResponse { AvatarName = invitee.Name };
        var roster = PartyBroadcast.BuildRoster(zones, joinOutcome == PartyJoinOutcome.Created ? 1 : 2, members);

        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var member))
            {
                member.Session.Send(joinNotice);
                member.Session.Send(roster);
            }
    }
}
