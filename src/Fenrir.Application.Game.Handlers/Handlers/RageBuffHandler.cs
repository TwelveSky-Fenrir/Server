using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class RageBuffHandler(ILogger<RageBuffHandler> logger)
    : IInlinePacketHandler<RageBuffRequest>
{
    public void Handle(in RageBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId?.ToString() ?? "?";

        logger.LogWarning(
            "Session {SessionId}: RageBuffRequest received for character {CharacterId} — op117 P_RAGE_BUFF_SEND has " +
            "no REGWORK1 line in legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server " +
            "logs Unknown Header and quits the session (:292-301); silently ignored",
            session.SessionId, characterId);
    }
}
