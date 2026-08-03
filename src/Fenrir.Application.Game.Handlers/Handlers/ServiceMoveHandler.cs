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
        var characterId = zoneSession.CharacterId?.ToString() ?? "?";

        logger.LogWarning(
            "Session {SessionId}: ServiceMoveRequest received for character {CharacterId} — op116 " +
            "P_SERVICE_MOVE_SEND has no REGWORK1 line in legacy MyWork::Init " +
            "(Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server logs Unknown Header and quits the " +
            "session (:292-301); replying with ReturnToHomeZone",
            session.SessionId, characterId);

        if (zoneSession.CurrentZone is not Zone)
            return;

        session.Send(new ReturnToHomeZoneResponse());
    }
}
