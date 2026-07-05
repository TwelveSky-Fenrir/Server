using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>
///     Business logic for op20, CZ_DEMAND_ZONE_SERVER_INFO_2 -- covers every zone-transfer reason the wire
///     distinguishes only by Sort (GM, death return, portal, paying NPC, teleport item, etc); see
///     <c>ZoneMoveHandler</c>'s own remarks for the full rationale, including ADR-0012's same-connection
///     intra-shard handoff design. Owns every send/abort/zone-command-post itself (rather than returning a
///     Result for the handler to translate) because success and failure are both threaded through several
///     interleaved, order-dependent session sends -- collapsing that into a single uniform result shape would
///     restructure control flow rather than merely relocate it.
/// </summary>
public interface IZoneMoveService
{
    public ValueTask HandleAsync(ZoneMoveRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
