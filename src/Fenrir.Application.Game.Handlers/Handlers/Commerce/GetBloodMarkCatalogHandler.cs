using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

public sealed class GetBloodMarkCatalogHandler(
    IGetBloodMarkCatalogService service,
    ILogger<GetBloodMarkCatalogHandler> logger) : IInlinePacketHandler<GetBloodMarkCatalogRequest>
{
    public void Handle(in GetBloodMarkCatalogRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("GetBloodMarkCatalog: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        session.Send(new GetBloodMarkCatalogResponse { Data = service.GetCatalog() });
    }
}
