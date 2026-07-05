using Fenrir.Application.Login.Pins;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op15 CL_LOGIN_MOUSE_PASSWORD_SEND — mismatch replies Result=1 and counts a strike; 3rd consecutive strike
///     disconnects (legacy GL_504).
/// </summary>
public sealed class VerifyMousePinHandler(IAccountPinRepository pins)
    : IAsyncPacketHandler<VerifyMousePinRequest>
{
    /// <summary>Legacy <c>mSecondLoginTryNum == 3</c> (S04_MyWork02.cpp l.568).</summary>
    private const int MaxPinFailures = 3;

    public async ValueTask HandleAsync(VerifyMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        // No stored PIN => Quit; client must create one first (op13).
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
        {
            loginSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (!MousePinFormat.IsValid(packet.MousePasswordInput))
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        if (!PasswordHasher.Verify(packet.MousePasswordInput, storedPin.PinHash, storedPin.PinSalt))
        {
            session.Send(new VerifyMousePinResponse { Result = 1 });
            if (loginSession.RegisterPinFailure() >= MaxPinFailures)
                loginSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        loginSession.MarkCharSelect();
        session.Send(new VerifyMousePinResponse { Result = 0 });
    }
}
