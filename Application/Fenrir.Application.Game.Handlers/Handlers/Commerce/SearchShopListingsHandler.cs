using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_PSHOP_ITEM_INFO_SEND (opcode 34) -- market-wide search across every live personal-shop stall
///     currently open in this zone, same-zone-only like <see cref="ViewShopStallHandler" />. One
///     <see cref="SearchShopListingsResponse" /> per matching listing (a burst, not a single reply).
/// </summary>
public sealed class SearchShopListingsHandler(ISearchShopListingsService service)
    : IInlinePacketHandler<SearchShopListingsRequest>
{
    public void Handle(in SearchShopListingsRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
            return;

        foreach (var response in service.Search(packet, zone))
            session.Send(response);
    }
}
