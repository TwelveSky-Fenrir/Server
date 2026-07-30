using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PetActionUpdateHandler(IPetActionUpdateService service, ILogger<PetActionUpdateHandler> logger)
    : IInlinePacketHandler<PetActionUpdateRequest>
{
    public void Handle(in PetActionUpdateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: PetActionUpdateRequest (op156) received for character {CharacterId}",
                session.SessionId, zoneSession.CharacterId);

        var action = packet.Action;
        service.Apply(zone, zoneSession.CharacterId!.Value, in action);
    }
}
