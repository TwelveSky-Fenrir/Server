using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SmeltItemHandler(ILogger<SmeltItemHandler> logger)
    : IInlinePacketHandler<SmeltItemRequest>
{
    public void Handle(in SmeltItemRequest packet, IPacketSession session)
    {
        logger.LogDebug(
            "Session {SessionId}: SmeltItemRequest received — USE_SOCKET_GEM system is dead in production, returning failure",
            session.SessionId);

        session.Send(new SmeltItemResponse { Result = 1, Cost = 0, Value = 0 });
    }
}
