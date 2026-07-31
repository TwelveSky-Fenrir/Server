using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class DuelCancelHandler(IDuelService duelService, ILogger<DuelCancelHandler>? logger = null)
    : IInlinePacketHandler<DuelCancelRequest>
{
    public void Handle(in DuelCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var challengerId = zoneSession.CharacterId!.Value;

        logger?.LogDebug("Duel cancel received: session {SessionId} character {CharacterId}", session.SessionId,
            challengerId);

        duelService.Cancel(challengerId);
    }
}
