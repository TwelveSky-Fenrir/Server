using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class GetProxyShopHandler(IGetProxyShopService service, ILogger<GetProxyShopHandler> logger)
    : IAsyncPacketHandler<GetProxyShopRequest>
{
    public async ValueTask HandleAsync(GetProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug("GetProxyShop: session {SessionId} character {CharacterId} sort {Sort} target {AvatarName}",
            session.SessionId, characterId, packet.Sort, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            logger.LogWarning(
                "Get proxy shop rejected: character {CharacterId} is outside the market district (zone {MapId}) -- session will be disconnected",
                characterId, zone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var response = await service.GetAsync(packet, zone, characterId, cancellationToken);
        session.Send(response);
    }
}
