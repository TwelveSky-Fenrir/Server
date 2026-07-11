using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class PartyLeaveHandler(
    ZoneRegistry zones,
    IPartyLeaveService partyLeaveService,
    IPartyResyncRelayQueue partyResyncRelay,
    IOptions<GameServerOptions> options,
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

        var shardId = options.Value.ShardId;

        var notice = new PartyLeaveResponse { AvatarName = leaver.Name };
        foreach (var memberId in result.MembersBeforeLeave)
            PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, memberId, notice,
                PartyResyncRelaySort.LeaveNotice, leaver.Name);

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
            if (memberId != characterId)
                PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, memberId, disbandNotice,
                    PartyResyncRelaySort.DisbandNotice, "");
    }
}
