using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Login.Packets;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class ChangeMousePinHandler(
    IChangeMousePinService changeMousePinService,
    ILogger<ChangeMousePinHandler> logger)
    : IAsyncPacketHandler<ChangeMousePinRequest>
{
    private const int MaxPinFailures = 3;

    private const string ZeroPin = "0000";

    public async ValueTask HandleAsync(ChangeMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Session {SessionId}: op14 CL_CHANGE_MOUSE_PASSWORD_SEND received for account {AccountId}",
                session.SessionId, accountId);

        var result = await changeMousePinService.ChangeMousePinAsync(accountId, packet.MousePassword,
            packet.ChangeMousePassword, cancellationToken);

        switch (result.Outcome)
        {
            case ChangeMousePinOutcome.NoPinConfigured:
                logger.LogWarning(
                    "PIN change rejected: account {AccountId} has no PIN configured yet -- aborting", accountId);
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case ChangeMousePinOutcome.InvalidFormat:
                logger.LogWarning("PIN change rejected: malformed PIN from account {AccountId} -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case ChangeMousePinOutcome.WrongPassword:
                session.Send(new ChangeMousePinResponse { Result = 1, MousePassword = ZeroPin });
                var failureCount = loginSession.RegisterPinFailure();
                var lockedOut = failureCount >= MaxPinFailures;
                await changeMousePinService.LogFailedAttemptAsync(accountId, failureCount, lockedOut,
                    cancellationToken);
                if (lockedOut)
                    loginSession.Abort(DisconnectReason.StateViolation);

                return;
            case ChangeMousePinOutcome.Locked:
                logger.LogWarning(
                    "PIN change rejected: account {AccountId} is locked out from PIN attempts -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case ChangeMousePinOutcome.StorageFailure:
                logger.LogWarning("PIN change failed to persist for account {AccountId} (storage failure)",
                    accountId);
                session.Send(new ChangeMousePinResponse { Result = 2, MousePassword = ZeroPin });
                return;
            default:
                loginSession.MarkCharSelect();
                logger.LogInformation("PIN changed for account {AccountId}; session moved to CharSelect", accountId);
                session.Send(new ChangeMousePinResponse { Result = 0, MousePassword = packet.ChangeMousePassword });
                return;
        }
    }
}
