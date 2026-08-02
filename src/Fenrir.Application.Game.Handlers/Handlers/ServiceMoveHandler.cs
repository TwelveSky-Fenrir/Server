using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ServiceMoveHandler(ILogger<ServiceMoveHandler> logger)
    : IInlinePacketHandler<ServiceMoveRequest>
{
    public void Handle(in ServiceMoveRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        if (zoneSession.CurrentZone is not Zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: ServiceMoveRequest received for character {CharacterId} — sending ReturnToHomeZone",
            session.SessionId, characterId);

        session.Send(new ReturnToHomeZoneResponse());
    }
}
