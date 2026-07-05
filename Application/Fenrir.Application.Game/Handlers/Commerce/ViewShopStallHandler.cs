using Fenrir.Application.Game.Handlers.Commerce.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>CZ_DEMAND_PSHOP_SEND (opcode 33) -- inspect another live personal shop stall, same-zone only.</summary>
public sealed class ViewShopStallHandler(IViewShopStallService service) : IInlinePacketHandler<ViewShopStallRequest>
{
    public void Handle(in ViewShopStallRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var requester) ||
            requester is null)
            return;

        if (zone.MapId != OpenShopStallHandler.PshopZoneNumber)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(service.View(packet, zone, requester));
    }
}
