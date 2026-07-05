using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_GET_ZONE_CONNECT_USER_SEND (opcode 92). Already customized in this fork: ignores the request's
///     <see cref="TribePopulationRequest.ZoneNumber" /> and always replies with 4 packets, one per tribe
///     (0-3), each carrying that tribe's live connected-player count across every zone of this process.
/// </summary>
public sealed class TribePopulationHandler(ITribePopulationService populationService)
    : IInlinePacketHandler<TribePopulationRequest>
{
    private const int BaseZoneNumber = 348;

    public void Handle(in TribePopulationRequest packet, IPacketSession session)
    {
        var counts = populationService.GetConnectedUserCounts();
        for (byte tribe = 0; tribe < counts.Count; tribe++)
            session.Send(new TribePopulationResponse
                { ZoneNumber = BaseZoneNumber + tribe, ConnectedUser = counts[tribe] });
    }
}
