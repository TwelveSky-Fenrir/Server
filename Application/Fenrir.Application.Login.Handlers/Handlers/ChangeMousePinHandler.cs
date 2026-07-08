using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op14 CL_CHANGE_MOUSE_PASSWORD_SEND — legacy quirk: a successful change also validates the PIN
///     (S04_MyWork02.cpp l.532), so the session goes straight to CharSelect.
/// </summary>
public sealed class ChangeMousePinHandler(
    IChangeMousePinService changeMousePinService,
    ILogger<ChangeMousePinHandler> logger)
    : IAsyncPacketHandler<ChangeMousePinRequest>
{
    private const int MaxPinFailures = 3;

    /// <summary>Legacy <c>c0000</c> — the mask echoed on every non-success reply of ops 13/14.</summary>
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
                // No stored PIN => Quit; client must create one first (op13).
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
                // ChangeMousePinService.LogFailedAttemptAsync itself already logs the operational Warning
                // line (mismatch vs. lockout) alongside the durable game.EventLog AccountSecurity row --
                // nothing further to add here beyond the Abort itself. Mirrors VerifyMousePinHandler's
                // identical shape (see that handler's own remarks).
                await changeMousePinService.LogFailedAttemptAsync(accountId, failureCount, lockedOut,
                    cancellationToken);
                if (lockedOut)
                    loginSession.Abort(DisconnectReason.StateViolation);

                return;
            case ChangeMousePinOutcome.Locked:
                // Fenrir-only account-scoped lockout (Migrations/028_account_pin_lockout.sql): silent abort,
                // matching NoPinConfigured/InvalidFormat's posture -- no wire reply, no legacy analog to
                // preserve. ChangeMousePinService already wrote the durable audit row for this rejection.
                logger.LogWarning(
                    "PIN change rejected: account {AccountId} is locked out from PIN attempts -- aborting",
                    accountId);
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case ChangeMousePinOutcome.StorageFailure:
                // Legacy: storage failure replies 2 without disconnecting (S04_MyWork02.cpp l.525-530). The
                // exception itself is already logged at ChangeMousePinService; this is just the outcome summary.
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
