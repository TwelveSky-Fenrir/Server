using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class GetCashCatalogHandler(IGetCashCatalogService service, ILogger<GetCashCatalogHandler> logger)
    : IInlinePacketHandler<GetCashCatalogRequest>
{
    public void Handle(in GetCashCatalogRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("GetCashCatalog: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        PlayerRuntimeState? state = null;
        if (zoneSession.CurrentZone is Zone zone && zoneSession.CharacterId is { } characterId)
            zone.TryGetPlayer(characterId, out state);

        session.Send(service.GetCatalog(state));
    }
}
