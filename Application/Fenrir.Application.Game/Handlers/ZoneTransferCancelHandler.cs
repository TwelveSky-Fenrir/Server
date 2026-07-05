using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op21, CZ_FAIL_MOVE_ZONE_2_SEND -- client reports it could not complete a zone transfer. Log-and-no-op:
///     Fenrir's intra-shard transfer (ADR-0012) never disconnects the client, so there is no state to unwind.
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
