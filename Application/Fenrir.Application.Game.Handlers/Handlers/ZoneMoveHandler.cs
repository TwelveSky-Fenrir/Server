using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op20, CZ_DEMAND_ZONE_SERVER_INFO_2 -- covers every zone-transfer reason the wire distinguishes only by
///     Sort (GM, death return, portal, paying NPC, teleport item, etc); wire-level validation doesn't vary by
///     reason beyond the Sort==2/GM case, so one handler covers all of them. Per-reason business rules the
///     legacy also enforces (portal proximity, PvP anti-revive-hack, GM rank) are an explicit scope cut.
///     Business logic lives in <see cref="IZoneMoveService" />.
/// </summary>
/// <remarks>
///     ADR-0012: unlike the legacy (client disconnects and reconnects to the target zone's own process), Fenrir
///     keeps the same TCP connection for an intra-shard transfer -- this handler resolves the destination,
///     replies with this shard's own ip:port, pushes a fresh world-state snapshot on this session, then hands
///     the character to the target zone actor, all in one shot.
///     Unverified against a real client: it may attempt its own reconnect upon receiving ip:port regardless of
///     whether it already names the current socket -- if so this simplification needs revisiting.
/// </remarks>
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
