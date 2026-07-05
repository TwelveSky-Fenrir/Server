using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>CZ_UPDATE_PET_ACTION_SEND (op156). No reply -- position rebroadcasts via ZC 15.</summary>
public sealed class PetActionUpdateHandler(IPetActionUpdateService service)
    : IInlinePacketHandler<PetActionUpdateRequest>
{
    public void Handle(in PetActionUpdateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        service.Apply(zone, zoneSession.CharacterId!.Value, in action);
    }
}
