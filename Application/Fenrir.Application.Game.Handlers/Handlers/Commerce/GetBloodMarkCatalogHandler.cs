using Fenrir.Application.Game.Abstractions.Commerce;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Commerce;

/// <summary>CZ_DEMAND_BLOOD_MARK_SEND (opcode 140) -- the blood-mark exchange catalog.</summary>
public sealed class GetBloodMarkCatalogHandler(
    IGetBloodMarkCatalogService service,
    ILogger<GetBloodMarkCatalogHandler> logger) : IInlinePacketHandler<GetBloodMarkCatalogRequest>
{
    public void Handle(in GetBloodMarkCatalogRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("GetBloodMarkCatalog: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        session.Send(new GetBloodMarkCatalogResponse { Data = service.GetCatalog() });
    }
}
