using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class PartyKickHandler(
    ZoneRegistry zones,
    IPartyKickService partyKickService,
    IPartyResyncRelayQueue partyResyncRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyKickHandler> logger) : IInlinePacketHandler<PartyKickRequest>
{
    public void Handle(in PartyKickRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("PartyKick: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        var leaderId = zoneSession.CharacterId!.Value;

        var result = partyKickService.Kick(leaderId, packet.AvatarName);
        if (result.Kind is PartyKickResultKind.NotLeader or PartyKickResultKind.TargetNotFound)
            return;

        var shardId = options.Value.ShardId;

        var notice = new PartyKickResponse { AvatarName = packet.AvatarName };
        foreach (var member in result.MembersBeforeKick)
            PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, member.CharacterId, notice,
                PartyResyncRelaySort.KickNotice, packet.AvatarName);

        if (!result.Disbanded)
        {
            if (result.RemainingMembers.Count > 0)
                foreach (var remaining in result.RemainingMembers)
                    PartyBroadcast.SendOrRelayRoster(zones, partyResyncRelay, shardId, remaining, 2,
                        result.RemainingMembers);

            return;
        }

        var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var member in result.MembersBeforeKick)
            if (member.CharacterId != result.TargetId)
                PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, member.CharacterId, disbandNotice,
                    PartyResyncRelaySort.DisbandNotice, "");
    }
}
