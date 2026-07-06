using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op15 CL_LOGIN_MOUSE_PASSWORD_SEND — mismatch replies Result=1 and counts a strike; 3rd consecutive strike
///     disconnects (legacy GL_504). Every mismatch is recorded as a game.EventLog AccountSecurity row (the
///     strike that crosses <see cref="MaxPinFailures" /> is flagged as a lockout); this audit trail is a new
///     Fenrir observability addition with no legacy analog, not a reproduced legacy behavior.
/// </summary>
public sealed class VerifyMousePinHandler(IVerifyMousePinService verifyMousePinService)
    : IAsyncPacketHandler<VerifyMousePinRequest>
{
    /// <summary>Legacy <c>mSecondLoginTryNum == 3</c> (S04_MyWork02.cpp l.568).</summary>
    private const int MaxPinFailures = 3;

    public async ValueTask HandleAsync(VerifyMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        var result = await verifyMousePinService.VerifyMousePinAsync(accountId, packet.MousePasswordInput,
            cancellationToken);

        switch (result.Outcome)
        {
            case VerifyMousePinOutcome.NoPinConfigured:
                // No stored PIN => Quit; client must create one first (op13).
                loginSession.Abort(DisconnectReason.StateViolation);
                return;
            case VerifyMousePinOutcome.InvalidFormat:
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
            default:
                loginSession.MarkCharSelect();
                session.Send(new VerifyMousePinResponse { Result = 0 });
                return;
        }
    }
}
