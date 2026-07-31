using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribePopulationHandler(
    ITribePopulationService populationService,
    ILogger<TribePopulationHandler>? logger = null) : IInlinePacketHandler<TribePopulationRequest>
{
    private const int BaseZoneNumber = 348;

    public void Handle(in TribePopulationRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug("Session {SessionId}: CZ_GET_ZONE_CONNECT_USER_SEND received", session.SessionId);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var counts = populationService.GetConnectedUserCounts(zone);
        for (byte tribe = 0; tribe < counts.Count; tribe++)
            session.Send(new TribePopulationResponse
                { ZoneNumber = BaseZoneNumber + tribe, ConnectedUser = counts[tribe] });
    }
}
