using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class EnterWorldHandler(IEnterWorldService service, ILogger<EnterWorldHandler>? logger = null)
    : IAsyncPacketHandler<EnterWorldRequest>
{
    public ValueTask HandleAsync(EnterWorldRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogInformation(
            "Session {SessionId}: EnterWorldRequest (op12) received for account {AccountId} character {CharacterId}",
            session.SessionId, zoneSession.AccountId, zoneSession.CharacterId);

        return service.HandleAsync(packet, zoneSession, cancellationToken);
    }
}
