using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class ChangeMasterHandler(ILogger<ChangeMasterHandler> logger) : IInlinePacketHandler<ChangeMasterRequest>
{
    public void Handle(in ChangeMasterRequest packet, IPacketSession session)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: op24 CL_CHANGE_MASTER_SEND received and ignored (legacy no-op feature)",
                session.SessionId);
    }
}
