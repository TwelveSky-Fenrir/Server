using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class ZoneMoveHandler(IZoneMoveService service, ILogger<ZoneMoveHandler>? logger = null)
    : IAsyncPacketHandler<ZoneMoveRequest>
{
    public ValueTask HandleAsync(ZoneMoveRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: ZoneMoveRequest (op20) received for character {CharacterId} -- target zone {TargetZone} (from {PresentZone}), sort {Sort}",
            session.SessionId, zoneSession.CharacterId, packet.ZoneNumber, packet.PresentZoneNumber, packet.Sort);

        return service.HandleAsync(packet, zoneSession, cancellationToken);
    }
}
