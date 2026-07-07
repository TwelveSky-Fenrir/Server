using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_PSHOP_ITEM_INFO_SEND (opcode 34) -- market-wide search: every currently open proxy/deputy shop
///     cluster-wide, unioned with every live personal-shop stall currently open in this zone only, gated
///     to zone 37 like <see cref="ViewShopStallHandler" />. One <see cref="SearchShopListingsResponse" />
///     per matching listing (a burst, not a single reply). Async -- the proxy-shop half is a database read.
/// </summary>
public sealed class SearchShopListingsHandler(ISearchShopListingsService service)
    : IAsyncPacketHandler<SearchShopListingsRequest>
{
    public async ValueTask HandleAsync(SearchShopListingsRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
            return;

        foreach (var response in await service.SearchAsync(packet, zone, cancellationToken))
            session.Send(response);
    }
}
