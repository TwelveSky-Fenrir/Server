using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_COSTUME_STATE2_SEND (op139). Sort must be strictly 0 or 1, else Quit(). Unlike op90, the AOI
///     broadcast here is a full avatar-action rebroadcast, not an AvatarStateFlag pair.
/// </summary>
public sealed class CostumeVisibilityHandler(ICostumeVisibilityService service)
    : IInlinePacketHandler<CostumeVisibilityRequest>
{
    public void Handle(in CostumeVisibilityRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (packet.Sort is not (0 or 1))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        session.Send(new CostumeVisibilityResponse { Sort = packet.Sort, Sort2 = 0, Sort3 = 0 });

        service.Apply(zone, characterId, packet.Sort);
    }
}
