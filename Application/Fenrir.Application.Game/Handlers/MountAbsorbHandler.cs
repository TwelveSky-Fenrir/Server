using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_ANIMAL_ABSORB_SEND (op113). No dedicated reply -- state changes broadcast via AVATAR_CHANGE_INFO_1
///     (AOI) + AVATAR_CHANGE_INFO_2 (self) instead, mirrored onto the tick through
///     <see cref="World.MountZoneCommand" />.
/// </summary>
public sealed class MountAbsorbHandler(IMountAbsorbService service) : IInlinePacketHandler<MountAbsorbRequest>
{
    public void Handle(in MountAbsorbRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        switch (packet.Sort)
        {
            case 1:
                if (!service.TryAbsorb(zone, state, characterId))
                    zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case 2:
                service.Release(zone, state, characterId);
                return;

            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}
