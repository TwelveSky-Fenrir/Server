using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class ViewShopStallHandler(IViewShopStallService service, ILogger<ViewShopStallHandler> logger)
    : IInlinePacketHandler<ViewShopStallRequest>
{
    public void Handle(in ViewShopStallRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug("ViewShopStall: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, characterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var requester) ||
            requester is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            logger.LogWarning(
                "View shop stall rejected: character {CharacterId} is outside the market district (zone {MapId}) -- session will be disconnected",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(service.View(packet, zone, requester));
    }
}
