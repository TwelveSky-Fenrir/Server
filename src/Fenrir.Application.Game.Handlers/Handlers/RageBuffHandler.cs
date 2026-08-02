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

        logger.LogDebug(
            "Session {SessionId}: RageBuffRequest received for character {CharacterId} — no rage state tracked, silently ignored",
            session.SessionId, characterId);
    }
}
