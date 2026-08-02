using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CostumeVisibilityHandler(
    ICostumeVisibilityService service,
    ILogger<CostumeVisibilityHandler> logger)
    : IInlinePacketHandler<CostumeVisibilityRequest>
{
    public void Handle(in CostumeVisibilityRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (packet.Sort is not (0 or 1))
        {
            logger.LogWarning(
                "Costume-visibility ignored for session {SessionId}: invalid sort {Sort}",
                session.SessionId, packet.Sort);
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
