using Fenrir.Application.Login.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class ZoneTransferFailureHandler(
    ISessionTicketRepository tickets,
    ILogger<ZoneTransferFailureHandler> logger)
    : IAsyncPacketHandler<ZoneTransferFailureRequest>
{
    public async ValueTask HandleAsync(ZoneTransferFailureRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        // Le rollback legacy n'est pas cosmetique : register_2 repasse l'emplacement de LP_MOVING_ZONE a
        // LP_UPDATE_USER (Server/ts25playuser/S07_MyGame01.cpp:968), etat depuis lequel la zone REFUSE la
        // reprise (S07_MyGame01.cpp:1057-1060). Le ticket doit donc mourir avant l'etat local.
        try
        {
            await tickets.RevokeAsync(accountId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Session {SessionId} (account {AccountId}): zone transfer rollback could not revoke the session " +
                "ticket -- closing the session rather than leaving a consumable hand-off behind",
                session.SessionId, accountId);
            loginSession.Abort(DisconnectReason.Faulted);
            return;
        }

        logger.LogInformation(
            "Session {SessionId} (account {AccountId}): zone transfer failed client-side -- ticket revoked, " +
            "rolling back to CharSelect",
            session.SessionId, accountId);

        loginSession.MarkCharSelect();
    }
}
