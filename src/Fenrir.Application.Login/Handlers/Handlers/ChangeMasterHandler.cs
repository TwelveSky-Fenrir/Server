using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

// Do not wire ChangeMasterResponse here: BEGIN_CL(CHANGE_MASTER_SEND) has an empty body and no emitter of
// LCP 27 exists anywhere under Server/ (Server/ts25login/S04_MyWork02.cpp:1643-1646). Answering is a divergence.
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
