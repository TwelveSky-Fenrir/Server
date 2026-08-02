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

        logger.LogDebug(
            "Session {SessionId}: PetCommandRequest received for character {CharacterId} (Sort {Sort}) — " +
            "op115 was never registered in legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:126-127), silently ignored",
            session.SessionId, characterId, packet.Sort);
    }
}
