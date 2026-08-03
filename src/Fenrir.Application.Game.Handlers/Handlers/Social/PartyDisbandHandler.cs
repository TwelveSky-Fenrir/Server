using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class PartyDisbandHandler(
    ZoneRegistry zones,
    IPartyDisbandService partyDisbandService,
    IPartyResyncRelayQueue partyResyncRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyDisbandHandler> logger) : IInlinePacketHandler<PartyDisbandRequest>
{
    public void Handle(in PartyDisbandRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("PartyDisband: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        var leaderId = zoneSession.CharacterId!.Value;

        var result = partyDisbandService.Disband(leaderId);
        if (result.Members.Count == 0)
            return;

        var shardId = options.Value.ShardId;

        var notice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var member in result.Members)
            PartyBroadcast.SendOrRelayNotice(zones, partyResyncRelay, shardId, member.CharacterId, notice,
                PartyResyncRelaySort.DisbandNotice, "");
    }
}
