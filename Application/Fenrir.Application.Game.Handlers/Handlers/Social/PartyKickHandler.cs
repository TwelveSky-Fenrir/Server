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

public sealed class PartyKickHandler(
    ZoneRegistry zones,
    IPartyKickService partyKickService,
    IPartyResyncRelayQueue partyResyncRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyKickHandler> logger) : IInlinePacketHandler<PartyKickRequest>
{
    public void Handle(in PartyKickRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("PartyKick: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        var leaderId = zoneSession.CharacterId!.Value;

        var result = partyKickService.Kick(leaderId, packet.AvatarName);
        if (result.Kind is PartyKickResultKind.NotLeader or PartyKickResultKind.TargetNotFound)
            return;

        var shardId = options.Value.ShardId;

        var notice = new PartyKickResponse { AvatarName = packet.AvatarName };
        foreach (var memberId in result.MembersBeforeKick)
            PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, memberId, notice,
                PartyResyncRelaySort.KickNotice, packet.AvatarName);

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
            if (memberId != result.TargetId)
                PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, memberId, disbandNotice,
                    PartyResyncRelaySort.DisbandNotice, "");
    }
}
