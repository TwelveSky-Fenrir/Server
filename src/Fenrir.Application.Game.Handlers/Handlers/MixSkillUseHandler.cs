using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class MixSkillUseHandler(ILogger<MixSkillUseHandler> logger)
    : IInlinePacketHandler<MixSkillUseRequest>
{
    public void Handle(in MixSkillUseRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        logger.LogWarning(
            "Session {SessionId}: rejected unregistered opcode {Opcode}", session.SessionId, 128);
        zoneSession.Abort(DisconnectReason.Faulted);
    }
}
