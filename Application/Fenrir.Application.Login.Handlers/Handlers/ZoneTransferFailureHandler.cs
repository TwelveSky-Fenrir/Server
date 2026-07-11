using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class ZoneTransferFailureHandler(ILogger<ZoneTransferFailureHandler> logger)
    : IInlinePacketHandler<ZoneTransferFailureRequest>
{
    public void Handle(in ZoneTransferFailureRequest packet, IPacketSession session)
    {
        var loginSession = (LoginClientSession)session;

        logger.LogInformation(
            "Session {SessionId} (account {AccountId}): zone transfer failed client-side -- rolling back to CharSelect",
            session.SessionId, loginSession.AccountId);

        loginSession.MarkCharSelect();
    }
}
