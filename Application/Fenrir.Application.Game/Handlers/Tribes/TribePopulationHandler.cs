using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_GET_ZONE_CONNECT_USER_SEND (opcode 92). Already customized in this fork: ignores the request's
///     <see cref="TribePopulationRequest.ZoneNumber" /> and always replies with 4 packets, one per tribe
///     (0-3), each carrying that tribe's live connected-player count across every zone of this process.
/// </summary>
public sealed class TribePopulationHandler(ZoneRegistry zones) : IInlinePacketHandler<TribePopulationRequest>
{
    private const int BaseZoneNumber = 348;
    private const int TribeCount = 4;

    public void Handle(in TribePopulationRequest packet, IPacketSession session)
    {
        for (byte tribe = 0; tribe < TribeCount; tribe++)
        {
            var connectedUsers = 0;
            foreach (var zone in zones.Zones)
            foreach (var player in zone.Players)
                if (player.Tribe == tribe)
                    connectedUsers++;

            session.Send(new TribePopulationResponse
                { ZoneNumber = BaseZoneNumber + tribe, ConnectedUser = connectedUsers });
        }
    }
}
