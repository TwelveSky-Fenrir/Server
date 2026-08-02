using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class SocketSlotInsertHandler(ILogger<SocketSlotInsertHandler> logger)
    : IInlinePacketHandler<SocketSlotInsertRequest>
{
    public void Handle(in SocketSlotInsertRequest packet, IPacketSession session)
    {
        logger.LogDebug(
            "Session {SessionId}: SocketSlotInsertRequest received — USE_SOCKET_GEM system is dead in production, returning failure",
            session.SessionId);

        session.Send(new SocketSlotInsertResponse { Result = 1, Value = [0, 0, 0] });
    }
}
