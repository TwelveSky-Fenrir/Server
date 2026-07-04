using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_ANSWER_SEND (opcode 67) -- on accept, collapses legacy's separate PARTY_JOIN/PARTY_INFO
///     emissions into one fan-out; a full party (<see cref="PartyJoinOutcome.PartyWasFull" />) is a silent no-op.
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
