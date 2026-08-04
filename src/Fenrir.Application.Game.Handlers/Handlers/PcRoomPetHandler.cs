using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class PcRoomPetHandler(ILogger<PcRoomPetHandler> logger)
    : IInlinePacketHandler<PcRoomPetRequest>
{
    public void Handle(in PcRoomPetRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 136);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
