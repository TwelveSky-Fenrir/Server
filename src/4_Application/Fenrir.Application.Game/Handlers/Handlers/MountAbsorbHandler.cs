using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class MountAbsorbHandler(IMountAbsorbService service, ILogger<MountAbsorbHandler> logger)
    : IInlinePacketHandler<MountAbsorbRequest>
{
    public void Handle(in MountAbsorbRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
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
                    logger.LogWarning(
                        "Mount-absorb rejected for character {CharacterId}: absorb not available -- aborting session",
                        characterId);
                    zoneSession.Abort(DisconnectReason.Faulted);
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
                    "Mount-absorb rejected for character {CharacterId}: invalid sort {Sort} -- aborting session",
                    characterId, packet.Sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}
