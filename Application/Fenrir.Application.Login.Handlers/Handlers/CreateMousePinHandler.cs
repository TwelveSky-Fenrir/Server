using Fenrir.Application.Login.Abstractions.CreateMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op13 CL_CREATE_MOUSE_PASSWORD_SEND — first-time PIN creation; stored hashed (never in clear, unlike legacy),
///     then opens char select.
/// </summary>
public sealed class CreateMousePinHandler(
    ICreateMousePinService createMousePinService,
    ILogger<CreateMousePinHandler> logger)
    : IAsyncPacketHandler<CreateMousePinRequest>
{
    public async ValueTask HandleAsync(CreateMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        // AllowedStates=[PinRequired] gates this past MarkAuthenticated, so AccountId is always set here.
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
                // Legacy: creating over an existing PIN is a protocol violation (client should send op15/op14 instead).
                logger.LogWarning(
                    "PIN creation rejected: account {AccountId} already has a PIN configured -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case CreateMousePinOutcome.StorageFailure:
                // Legacy: storage failure is a silent Quit(), no reply (S04_MyWork02.cpp l.476-479). The
                // exception itself is already logged at CreateMousePinService; this is just the outcome summary.
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
