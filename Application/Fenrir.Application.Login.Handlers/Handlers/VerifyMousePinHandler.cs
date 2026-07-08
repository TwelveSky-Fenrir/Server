using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op15 CL_LOGIN_MOUSE_PASSWORD_SEND — mismatch replies Result=1 and counts a strike; 3rd consecutive strike
///     disconnects (legacy GL_504). Every mismatch is recorded as a game.EventLog AccountSecurity row (the
///     strike that crosses <see cref="MaxPinFailures" /> is flagged as a lockout); this audit trail is a new
///     Fenrir observability addition with no legacy analog, not a reproduced legacy behavior.
/// </summary>
public sealed class VerifyMousePinHandler(
    IVerifyMousePinService verifyMousePinService,
    ILogger<VerifyMousePinHandler> logger)
    : IAsyncPacketHandler<VerifyMousePinRequest>
{
    /// <summary>Legacy <c>mSecondLoginTryNum == 3</c> (S04_MyWork02.cpp l.568).</summary>
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
                // No stored PIN => Quit; client must create one first (op13).
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
                // VerifyMousePinService.LogFailedAttemptAsync itself already logs the operational Warning line
                // (mismatch vs. lockout) alongside the durable game.EventLog AccountSecurity row -- nothing
                // further to add here beyond the Abort itself.
                await verifyMousePinService.LogFailedAttemptAsync(accountId, failureCount, lockedOut,
                    cancellationToken);
                if (lockedOut)
                    loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case VerifyMousePinOutcome.Locked:
                // Fenrir-only account-scoped lockout (Migrations/028_account_pin_lockout.sql): silent abort,
                // matching NoPinConfigured/InvalidFormat's posture -- no wire reply, no legacy analog to
                // preserve. VerifyMousePinService already wrote the durable audit row for this rejection.
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
