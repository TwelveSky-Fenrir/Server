using Fenrir.Application.Game.Avatars;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

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
public sealed class ZoneMoveHandler(IZoneMoveService service) : IAsyncPacketHandler<ZoneMoveRequest>
{
    public ValueTask HandleAsync(ZoneMoveRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        return service.HandleAsync(packet, (ZoneClientSession)session, cancellationToken);
    }
}
