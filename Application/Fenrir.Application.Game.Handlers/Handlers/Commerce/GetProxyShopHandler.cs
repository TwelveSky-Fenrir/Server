using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_GET_DEPUTY_PSHOP_SEND (opcode 108) -- fetch a deputy (offline/proxy) shop's contents. Gated to
///     zone 37.
/// </summary>
public sealed class GetProxyShopHandler(IGetProxyShopService service, ILogger<GetProxyShopHandler> logger)
    : IAsyncPacketHandler<GetProxyShopRequest>
{
    public async ValueTask HandleAsync(GetProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
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
