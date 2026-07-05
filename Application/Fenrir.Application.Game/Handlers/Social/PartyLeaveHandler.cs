using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_LEAVE_SEND (opcode 69) -- no-op if not partied or is the leader (leader must use
///     CZ_PARTY_BREAK_SEND). Deviation: dropping to 1 member auto-disbands here; legacy leaves a lone
///     leader "partied" until an explicit Break.
/// </summary>
public sealed class PartyLeaveHandler(ZoneRegistry zones, PartyRegistry parties)
    : IInlinePacketHandler<PartyLeaveRequest>
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

        var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in membersBeforeLeave)
            if (memberId != characterId && zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(disbandNotice);
    }
}
