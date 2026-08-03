using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PetCommandHandler(ILogger<PetCommandHandler> logger)
    : IInlinePacketHandler<PetCommandRequest>
{
    public void Handle(in PetCommandRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId?.ToString() ?? "?";

        logger.LogWarning(
            "Session {SessionId}: PetCommandRequest received for character {CharacterId} (Sort {Sort}) — op115 " +
            "P_PAT_ACTION_SEND has no REGWORK1 line in legacy MyWork::Init " +
            "(Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server logs Unknown Header and quits the " +
            "session (:292-301); silently ignored",
            session.SessionId, characterId, packet.Sort);
    }
}
