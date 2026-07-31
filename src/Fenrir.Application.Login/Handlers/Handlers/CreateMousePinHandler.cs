using Fenrir.Application.Login.Abstractions.CreateMousePin;
using Fenrir.Application.Login.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class CreateMousePinHandler(
    ICreateMousePinService createMousePinService,
    ILogger<CreateMousePinHandler> logger)
    : IAsyncPacketHandler<CreateMousePinRequest>
{
    public async ValueTask HandleAsync(CreateMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        var accountId = loginSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: op13 CL_CREATE_MOUSE_PASSWORD_SEND received for account {AccountId}",
                session.SessionId, accountId);

        var result = await createMousePinService.CreateMousePinAsync(accountId, packet.MousePassword,
            cancellationToken);

        switch (result.Outcome)
        {
            case CreateMousePinOutcome.InvalidFormat:
                logger.LogWarning("PIN creation rejected: malformed PIN from account {AccountId} -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case CreateMousePinOutcome.AlreadyExists:
                logger.LogWarning(
                    "PIN creation rejected: account {AccountId} already has a PIN configured -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case CreateMousePinOutcome.StorageFailure:
                logger.LogWarning("PIN creation failed to persist for account {AccountId} -- aborting", accountId);
                loginSession.Abort(DisconnectReason.Faulted);
                return;
            default:
                loginSession.MarkCharSelect();
                logger.LogInformation("PIN created for account {AccountId}; session moved to CharSelect", accountId);
                session.Send(new CreateMousePinResponse { Result = 0, MousePassword = packet.MousePassword });
                return;
        }
    }
}
