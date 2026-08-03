using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class TrapCheckHandler(ILogger<TrapCheckHandler> logger)
    : IInlinePacketHandler<TrapCheckRequest>
{
    public void Handle(in TrapCheckRequest packet, IPacketSession session)
    {
        logger.LogWarning(
            "Session {SessionId}: TrapCheckRequest received for trap index {TrapIndex} — op106 P_TRAP_CHECK_SEND has " +
            "no REGWORK1 line in legacy MyWork::Init (Server/ts25zone/S04_MyWork01.cpp:6-266), so the legacy server " +
            "logs Unknown Header and quits the session (:292-301); replying with a canned empty response",
            session.SessionId, packet.TrapIndex);

        session.Send(new TrapPositionResponse { Result = 0, Value = 0 });
    }
}
