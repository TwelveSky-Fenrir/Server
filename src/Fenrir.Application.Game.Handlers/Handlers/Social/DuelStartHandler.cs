using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class DuelStartHandler(IDuelService duelService, ILogger<DuelStartHandler>? logger = null)
    : IInlinePacketHandler<DuelStartRequest>
{
    public void Handle(in DuelStartRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        logger?.LogDebug("Duel start received: session {SessionId} character {CharacterId}", session.SessionId,
            callerId);

        duelService.Start(callerId);
    }
}
