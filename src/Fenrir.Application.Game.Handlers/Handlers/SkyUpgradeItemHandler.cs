using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SkyUpgradeItemHandler(ILogger<SkyUpgradeItemHandler> logger)
    : IInlinePacketHandler<SkyUpgradeItemRequest>
{
    public void Handle(in SkyUpgradeItemRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 93);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
