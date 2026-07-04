using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_LEAVE_SEND (opcode 69). Ignored (silent) if the caller isn't partied, OR IS the leader
///     (the leader must use CZ_PARTY_BREAK_SEND instead, verified). Notifies every member who was present
///     BEFORE the departure (the leaver included -- matches ts25center's own case 106 loop, which fires
///     before the leaver's own party name is cleared). Mission brief: if the departure drops membership
///     to 1, the party auto-disbands (<see cref="PartyRegistry" />'s own remarks on this being a
///     deliberate, brief-directed rule -- the legacy itself leaves a lone leader "partied" until an
///     explicit Break).
/// </summary>
public sealed class PartyLeaveHandler(ZoneRegistry zones, PartyRegistry parties) : IInlinePacketHandler<PartyLeaveRequest>
{
    public void Handle(in PartyLeaveRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (!zones.TryGetPlayer(characterId, out var leaver))
            return;

        if (!parties.TryLeave(characterId, out var membersBeforeLeave, out var disbanded))
            return;

        var notice = new PartyLeaveResponse { AvatarName = leaver.Name };
        foreach (var memberId in membersBeforeLeave)
            if (zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(notice);

        if (!disbanded)
        {
            var remaining = parties.GetMembers(membersBeforeLeave[0]);
            if (remaining.Count > 0)
            {
                var roster = PartyBroadcast.BuildRoster(zones, 3, remaining);
                foreach (var memberId in remaining)
                    if (zones.TryGetPlayer(memberId, out var member))
                        member.Session.Send(roster);
            }

            return;
        }

        // Dropped to 1 member -- notify whoever is left that the party dissolved (mission-brief rule).
        var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in membersBeforeLeave)
            if (memberId != characterId && zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(disbandNotice);
    }
}
