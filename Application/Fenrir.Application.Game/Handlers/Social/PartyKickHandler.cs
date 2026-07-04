using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_EXILE_SEND (opcode 70). Reserved to the party LEADER (silent no-op otherwise, contract's
///     own "réservé au CHEF"). A self-targeted kick is not specially guarded here either (see
///     <see cref="Party.TryRemoveMember" />'s own remarks) -- faithfully reproduces the legacy's own lack
///     of such a guard.
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
            // Look up the roster via ANY surviving member, not necessarily leaderId -- a self-targeted
            // kick (targetId == leaderId, not specially guarded, see class remarks) would otherwise make
            // this lookup miss, since leaderId itself was just removed from the roster index.
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
