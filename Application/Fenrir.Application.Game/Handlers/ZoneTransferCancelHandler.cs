using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op21, CZ_FAIL_MOVE_ZONE_2_SEND (contracts/01_replication_combat.md) -- the client reports that it could
///     not complete a zone transfer it was just told to make (<see cref="ZoneMoveHandler" />'s
///     Result=0 reply). Minimal, log-and-no-op implementation: with ADR-0012 there is nothing server-side left
///     to roll back. Legacy behavior for reference (contract doc, S04_MyWork02.cpp:2189): resets
///     <c>mMoveZoneResult = 0</c> and re-registers the session with ts25playuser as "back on the login server",
///     <c>Quit()</c>-ing on re-registration failure -- both steps existed only because the legacy transfer
///     spans TWO processes and TWO TCP connections (the client disconnects and reconnects); Fenrir's intra-shard
///     transfer (ADR-0012) never disconnects the client and never leaves a "moving zone" flag armed for this to
///     clear, so there is no analogous state to unwind here. A real client should not normally be ABLE to send
///     this after a Fenrir transfer already completed synchronously within one tick, but accepting it as a
///     harmless no-op (rather than rejecting the opcode outright) costs nothing and matches the legacy's own
///     "no wire response" contract.
/// </summary>
public sealed class ZoneTransferCancelHandler(ILogger<ZoneTransferCancelHandler> logger)
    : IInlinePacketHandler<ZoneTransferCancelRequest>
{
    public void Handle(in ZoneTransferCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogInformation(
            "Character {CharacterId} sent CZ_FAIL_MOVE_ZONE_2_SEND -- no-op under ADR-0012 (see class remarks)",
            zoneSession.CharacterId);
    }
}
