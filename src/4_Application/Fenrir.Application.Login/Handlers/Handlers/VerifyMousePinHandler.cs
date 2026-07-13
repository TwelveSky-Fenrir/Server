using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Login.Packets;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class VerifyMousePinHandler(
    IVerifyMousePinService verifyMousePinService,
    ILogger<VerifyMousePinHandler> logger)
    : IAsyncPacketHandler<VerifyMousePinRequest>
{
    private const int MaxPinFailures = 3;

    public async ValueTask HandleAsync(VerifyMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: op15 CL_LOGIN_MOUSE_PASSWORD_SEND received for account {AccountId}",
                session.SessionId, accountId);

        var result = await verifyMousePinService.VerifyMousePinAsync(accountId, packet.MousePasswordInput,
            cancellationToken);

        switch (result.Outcome)
        {
            case VerifyMousePinOutcome.NoPinConfigured:
                logger.LogWarning(
                    "PIN verify rejected: account {AccountId} has no PIN configured yet -- aborting", accountId);
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case VerifyMousePinOutcome.InvalidFormat:
                logger.LogWarning("PIN verify rejected: malformed PIN from account {AccountId} -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case VerifyMousePinOutcome.WrongPassword:
                session.Send(new VerifyMousePinResponse { Result = 1 });
                var failureCount = loginSession.RegisterPinFailure();
                var lockedOut = failureCount >= MaxPinFailures;
                await verifyMousePinService.LogFailedAttemptAsync(accountId, failureCount, lockedOut,
                    cancellationToken);
                if (lockedOut)
                    loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case VerifyMousePinOutcome.Locked:
                logger.LogWarning(
                    "PIN verify rejected: account {AccountId} is locked out from PIN attempts -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            default:
                loginSession.MarkCharSelect();
                logger.LogInformation("PIN verified for account {AccountId}; session moved to CharSelect",
                    accountId);
                session.Send(new VerifyMousePinResponse { Result = 0 });
                return;
        }
    }
}
