using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op23 CL_FAIL_MOVE_ZONE_1_SEND — rolls back to CharSelect, no reply; the stale session ticket needs no explicit
///     revocation (single-use, short TTL).
/// </summary>
public sealed class ZoneTransferFailureHandler(ILogger<ZoneTransferFailureHandler> logger)
    : IInlinePacketHandler<ZoneTransferFailureRequest>
{
    public void Handle(in ZoneTransferFailureRequest packet, IPacketSession session)
    {
        var loginSession = (LoginClientSession)session;

        // Information, not Debug: this is a client-reported failure to reach the zone it was just handed off
        // to -- an operator watching live logs would want this visible by default (it's the client-side signal
        // that a zone-transfer handoff didn't complete), not routine per-packet chatter.
        logger.LogInformation(
            "Session {SessionId} (account {AccountId}): zone transfer failed client-side -- rolling back to CharSelect",
            session.SessionId, loginSession.AccountId);

        loginSession.MarkCharSelect();
    }
}
