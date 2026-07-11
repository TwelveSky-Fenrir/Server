using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

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
