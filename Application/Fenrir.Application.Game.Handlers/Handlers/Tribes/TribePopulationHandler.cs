using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

/// <summary>
///     CZ_GET_ZONE_CONNECT_USER_SEND (opcode 92). Already customized in this fork: ignores the request's
///     <see cref="TribePopulationRequest.ZoneNumber" /> and always replies with 4 packets, one per tribe
///     (0-3), each carrying that tribe's live connected-player count on the requester's own zone/map only --
///     matching the legacy one-process-per-map semantics (see the TribePopulation behavior contract), not a
///     shard-wide or cluster-wide figure.
/// </summary>
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
