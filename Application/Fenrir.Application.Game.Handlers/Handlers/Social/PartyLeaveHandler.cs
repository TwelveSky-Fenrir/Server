using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_PARTY_LEAVE_SEND (opcode 69) -- no-op if not partied or is the leader (leader must use
///     CZ_PARTY_BREAK_SEND). Deviation: dropping to 1 member auto-disbands here; legacy leaves a lone
///     leader "partied" until an explicit Break.
/// </summary>
public sealed class PartyLeaveHandler(
    ZoneRegistry zones,
    IPartyLeaveService partyLeaveService,
    ILogger<PartyLeaveHandler> logger) : IInlinePacketHandler<PartyLeaveRequest>
{
    public void Handle(in PartyLeaveRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("PartyLeave: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        var characterId = zoneSession.CharacterId!.Value;

        if (!zones.TryGetPlayer(characterId, out var leaver))
            return;

        var result = partyLeaveService.Leave(characterId);
        if (!result.Handled)
            return;

        var notice = new PartyLeaveResponse { AvatarName = leaver.Name };
        foreach (var memberId in result.MembersBeforeLeave)
            if (zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(notice);

        if (!result.Disbanded)
        {
            if (result.RemainingMembers.Count > 0)
            {
                var roster = PartyBroadcast.BuildRoster(zones, 3, result.RemainingMembers);
                foreach (var memberId in result.RemainingMembers)
                    if (zones.TryGetPlayer(memberId, out var member))
                        member.Session.Send(roster);
            }

            return;
        }

        var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in result.MembersBeforeLeave)
            if (memberId != characterId && zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(disbandNotice);
    }
}
