using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class TrapCheckHandler(ILogger<TrapCheckHandler> logger)
    : IInlinePacketHandler<TrapCheckRequest>
{
    public void Handle(in TrapCheckRequest packet, IPacketSession session)
    {
        logger.LogDebug(
            "Session {SessionId}: TrapCheckRequest for trap index {TrapIndex} — no trap system implemented, returning empty response",
            session.SessionId, packet.TrapIndex);

        session.Send(new TrapPositionResponse { Result = 0, Value = 0 });
    }
}
