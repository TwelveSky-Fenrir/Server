using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class PartyDisbandHandler(
    ZoneRegistry zones,
    IPartyDisbandService partyDisbandService,
    ILogger<PartyDisbandHandler> logger) : IInlinePacketHandler<PartyDisbandRequest>
{
    public void Handle(in PartyDisbandRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("PartyDisband: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        var leaderId = zoneSession.CharacterId!.Value;

        var result = partyDisbandService.Disband(leaderId);
        if (result.Members.Count == 0)
            return;

        var notice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in result.Members)
            if (zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(notice);
    }
}
