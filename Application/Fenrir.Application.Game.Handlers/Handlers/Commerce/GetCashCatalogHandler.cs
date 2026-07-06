using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>
///     CZ_GET_CASH_ITEM_INFO_SEND (opcode 91) -- the cash-shop catalog. Legacy only replies when the client's
///     cached version differs; Fenrir always replies with the current catalog instead (harmless -- see
///     <see cref="IGetCashCatalogService" />'s own remarks).
/// </summary>
public sealed class GetCashCatalogHandler(IGetCashCatalogService service) : IInlinePacketHandler<GetCashCatalogRequest>
{
    public void Handle(in GetCashCatalogRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        // Benign staleness window around world entry/transfer, same posture as HeartbeatHandler/ZoneReadyHandler:
        // if the player isn't resolvable yet, the catalog is still sent, just with nothing to record the
        // returned version onto.
        PlayerRuntimeState? state = null;
        if (zoneSession.CurrentZone is Zone zone && zoneSession.CharacterId is { } characterId)
            zone.TryGetPlayer(characterId, out state);

        session.Send(service.GetCatalog(state));
    }
}
