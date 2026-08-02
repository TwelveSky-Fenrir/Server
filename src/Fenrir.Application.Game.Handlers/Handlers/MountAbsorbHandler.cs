using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class MountAbsorbHandler(IMountAbsorbService service, ILogger<MountAbsorbHandler> logger)
    : IInlinePacketHandler<MountAbsorbRequest>
{
    public void Handle(in MountAbsorbRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: MountAbsorbRequest (op113) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        switch (packet.Sort)
        {
            case 1:
                if (!service.TryAbsorb(zone, state, characterId))
                {
                    logger.LogDebug(
                        "Mount-absorb ignored for character {CharacterId}: absorb not available (not mounted, or absorb time depleted)",
                        characterId);
                    return;
                }

                logger.LogInformation("Character {CharacterId} absorbed mount", characterId);
                return;

            case 2:
                service.Release(zone, state, characterId);
                logger.LogInformation("Character {CharacterId} released absorbed mount", characterId);
                return;

            default:
                logger.LogWarning(
                    "Mount-absorb ignored for character {CharacterId}: invalid sort {Sort}",
                    characterId, packet.Sort);
                return;
        }
    }
}
