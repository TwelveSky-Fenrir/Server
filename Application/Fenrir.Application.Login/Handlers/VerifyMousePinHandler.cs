using Fenrir.Application.Login.Handlers.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op15 CL_LOGIN_MOUSE_PASSWORD_SEND — mismatch replies Result=1 and counts a strike; 3rd consecutive strike
///     disconnects (legacy GL_504).
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
                if (loginSession.RegisterPinFailure() >= MaxPinFailures)
                    loginSession.Abort(DisconnectReason.StateViolation);
                return;
            default:
                loginSession.MarkCharSelect();
                session.Send(new VerifyMousePinResponse { Result = 0 });
                return;
        }
    }
}
