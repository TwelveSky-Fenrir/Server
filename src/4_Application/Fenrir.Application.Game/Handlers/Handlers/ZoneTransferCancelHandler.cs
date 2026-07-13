using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ZoneTransferCancelHandler(
    IZoneTransferCancelService service,
    ILogger<ZoneTransferCancelHandler>? logger = null) : IAsyncPacketHandler<ZoneTransferCancelRequest>
{
    public ValueTask HandleAsync(ZoneTransferCancelRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: ZoneTransferCancelRequest (op21) received for character {CharacterId}",
            session.SessionId, zoneSession.CharacterId);

        return service.HandleAsync(zoneSession, cancellationToken);
    }
}
