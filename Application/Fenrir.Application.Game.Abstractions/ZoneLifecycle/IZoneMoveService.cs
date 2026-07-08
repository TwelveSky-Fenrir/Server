using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

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
/// <remarks>
///     A target zone number absent from the caller's own shard is resolved live against
///     <c>runtime.GameServerDirectory</c> + <c>admin.ShardMapAssignments</c> before falling back to the
///     legacy directory-sentinel failure (Server/ts25zone/S04_MyWork02.cpp:2143-2147, port==0) -- legacy
///     itself never distinguishes "hosted by the requester's own process" from "hosted elsewhere" (every
///     zone is its own process, resolved against the same shared directory,
///     Server/ts25center/S04_MyWork02.cpp:74-109), so a live-but-different shard must reply with a real
///     ip:port (Result=0), not the same failure code as a genuinely unregistered zone process. That reply is
///     preceded by minting a fresh, destination-shard-scoped, single-use session ticket
///     (<c>ISessionTicketRepository.CreateAsync</c>) so the destination shard's own
///     <c>ZoneHandshakeService.ConsumeTicketAsync</c> has something to consume the moment the client
///     reconnects there -- the same ticket mechanism Login's <c>ZoneTransferService</c> already mints for the
///     first Login-&gt;Game handoff, reused here for a Game-&gt;Game one.
/// </remarks>
public interface IZoneMoveService
{
    public ValueTask HandleAsync(ZoneMoveRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}
