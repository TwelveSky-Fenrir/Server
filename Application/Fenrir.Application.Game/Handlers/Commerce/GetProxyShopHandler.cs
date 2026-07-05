using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     CZ_GET_DEPUTY_PSHOP_SEND (opcode 108) -- fetch a deputy (offline/proxy) shop's contents. Gated to
///     zone 37.
/// </summary>
public sealed class GetProxyShopHandler(IGetProxyShopService service) : IAsyncPacketHandler<GetProxyShopRequest>
{
    public async ValueTask HandleAsync(GetProxyShopRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var response = await service.GetAsync(packet, zone, characterId, cancellationToken);
        session.Send(response);
    }
}
