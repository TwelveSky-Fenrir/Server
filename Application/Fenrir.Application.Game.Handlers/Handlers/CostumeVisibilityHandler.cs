using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_COSTUME_STATE2_SEND (op139). Sort must be strictly 0 or 1, else Quit(). Unlike op90, the AOI
///     broadcast here is a full avatar-action rebroadcast, not an AvatarStateFlag pair.
/// </summary>
public sealed class CostumeVisibilityHandler(
    ICostumeVisibilityService service,
    ILogger<CostumeVisibilityHandler> logger)
    : IInlinePacketHandler<CostumeVisibilityRequest>
{
    public void Handle(in CostumeVisibilityRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (packet.Sort is not (0 or 1))
        {
            logger.LogWarning(
                "Costume-visibility rejected for session {SessionId}: invalid sort {Sort} -- aborting session",
                session.SessionId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: CostumeVisibilityRequest (op139) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        session.Send(new CostumeVisibilityResponse { Sort = packet.Sort, Sort2 = 0, Sort3 = 0 });

        logger.LogInformation("Character {CharacterId} set costume visibility to {Sort}", characterId, packet.Sort);

        service.Apply(zone, characterId, packet.Sort);
    }
}
