using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class GetCashCatalogHandler(IGetCashCatalogService service, ILogger<GetCashCatalogHandler> logger)
    : IInlinePacketHandler<GetCashCatalogRequest>
{
    public void Handle(in GetCashCatalogRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("GetCashCatalog: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        PlayerRuntimeState? state = null;
        if (zoneSession.CurrentZone is Zone zone && zoneSession.CharacterId is { } characterId)
            zone.TryGetPlayer(characterId, out state);

        session.Send(service.GetCatalog(state));
    }
}
