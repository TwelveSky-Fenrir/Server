using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class SearchShopListingsHandler(
    ISearchShopListingsService service,
    ILogger<SearchShopListingsHandler> logger) : IAsyncPacketHandler<SearchShopListingsRequest>
{
    public async ValueTask HandleAsync(SearchShopListingsRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("SearchShopListings: session {SessionId} character {CharacterId} sort1 {Sort1} sort2 {Sort2}",
            session.SessionId, zoneSession.CharacterId, packet.Sort1, packet.Sort2);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
            return;

        var responses = await service.SearchAsync(packet, zone, cancellationToken);

        logger.LogDebug("SearchShopListings: session {SessionId} returned {ResultCount} listings",
            session.SessionId, responses.Count);

        foreach (var response in responses)
            session.Send(response);
    }
}
