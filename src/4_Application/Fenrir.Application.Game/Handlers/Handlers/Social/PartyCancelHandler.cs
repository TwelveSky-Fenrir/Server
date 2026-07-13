using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class PartyCancelHandler(
    ZoneRegistry zones,
    IPartyCancelService partyCancelService,
    ILogger<PartyCancelHandler> logger) : IInlinePacketHandler<PartyCancelRequest>
{
    public void Handle(in PartyCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("PartyCancel: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        var inviterId = zoneSession.CharacterId!.Value;

        var result = partyCancelService.Cancel(inviterId);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.InviteeId, out var invitee))
            invitee.Session.Send(new PartyCancelResponse());
    }
}
