using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_EXILE_SEND (opcode 70) -- a self-targeted kick isn't specially guarded, matching legacy's
///     own lack of a guard.
/// </summary>
public sealed class PartyKickHandler(ZoneRegistry zones, PartyRegistry parties) : IInlinePacketHandler<PartyKickRequest>
{
    public void Handle(in PartyKickRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var leaderId = zoneSession.CharacterId!.Value;

        if (!parties.IsLeader(leaderId))
            return;

        var currentMembers = parties.GetMembers(leaderId);
        var targetId = 0;
        foreach (var memberId in currentMembers)
            if (zones.TryGetPlayer(memberId, out var member) &&
                string.Equals(member.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                targetId = memberId;
                break;
            }

        if (targetId == 0 || !parties.TryKick(leaderId, targetId, out var membersBeforeKick, out var disbanded))
            return;

        var notice = new PartyKickResponse { AvatarName = packet.AvatarName };
        foreach (var memberId in membersBeforeKick)
            if (zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(notice);

        if (!disbanded)
        {
            // Anchor on a surviving member, not leaderId: a self-kick removes leaderId from the roster index too.
            var anchor = membersBeforeKick.FirstOrDefault(id => id != targetId);
            var remaining = parties.GetMembers(anchor);
            if (remaining.Count > 0)
            {
                var roster = PartyBroadcast.BuildRoster(zones, 2, remaining);
                foreach (var memberId in remaining)
                    if (zones.TryGetPlayer(memberId, out var member))
                        member.Session.Send(roster);
            }

            return;
        }

        var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in membersBeforeKick)
            if (memberId != targetId && zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(disbandNotice);
    }
}
