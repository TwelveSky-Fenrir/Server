using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_EXILE_SEND (opcode 70) -- a self-targeted kick isn't specially guarded, matching legacy's
///     own lack of a guard.
/// </summary>
public sealed class PartyKickHandler(ZoneRegistry zones, IPartyKickService partyKickService)
    : IInlinePacketHandler<PartyKickRequest>
{
    public void Handle(in PartyKickRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var leaderId = zoneSession.CharacterId!.Value;

        var result = partyKickService.Kick(leaderId, packet.AvatarName);
        if (result.Kind is PartyKickResultKind.NotLeader or PartyKickResultKind.TargetNotFound)
            return;

        var notice = new PartyKickResponse { AvatarName = packet.AvatarName };
        foreach (var memberId in result.MembersBeforeKick)
            if (zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(notice);

        if (!result.Disbanded)
        {
            if (result.RemainingMembers.Count > 0)
            {
                var roster = PartyBroadcast.BuildRoster(zones, 2, result.RemainingMembers);
                foreach (var memberId in result.RemainingMembers)
                    if (zones.TryGetPlayer(memberId, out var member))
                        member.Session.Send(roster);
            }

            return;
        }

        var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in result.MembersBeforeKick)
            if (memberId != result.TargetId && zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(disbandNotice);
    }
}
