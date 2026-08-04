using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
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
        var zoneSession = (IZoneSession)session;

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
        foreach (var member in result.MembersBeforeLeave)
            PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, member.CharacterId, notice,
                PartyResyncRelaySort.LeaveNotice, leaver.Name);

        if (!result.Disbanded)
        {
            if (result.RemainingMembers.Count > 0)
                foreach (var remaining in result.RemainingMembers)
                    PartyBroadcast.SendOrRelayRoster(zones, partyResyncRelay, shardId, remaining, 3,
                        result.RemainingMembers);

            return;
        }

        var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var member in result.MembersBeforeLeave)
            if (member.CharacterId != characterId)
                PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, member.CharacterId, disbandNotice,
                    PartyResyncRelaySort.DisbandNotice, "");
    }
}
