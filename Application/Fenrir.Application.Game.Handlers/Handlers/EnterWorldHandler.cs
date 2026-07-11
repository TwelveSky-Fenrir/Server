using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class EnterWorldHandler(IEnterWorldService service, ILogger<EnterWorldHandler>? logger = null)
    : IAsyncPacketHandler<EnterWorldRequest>
{
    public ValueTask HandleAsync(EnterWorldRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogInformation(
            "Session {SessionId}: EnterWorldRequest (op12) received for account {AccountId} character {CharacterId}",
            session.SessionId, zoneSession.AccountId, zoneSession.CharacterId);

        return service.HandleAsync(packet, zoneSession, cancellationToken);
    }
}
