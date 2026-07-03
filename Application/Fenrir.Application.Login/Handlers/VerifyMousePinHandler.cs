using Fenrir.Application.Login.Pins;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Domain.Security;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op15 CL_LOGIN_MOUSE_PASSWORD_SEND — mouse-PIN verification (login protocol report §4.15), the mandatory
///     path for an account that already has a PIN in prod EU33. Legacy: no PIN in storage or invalid format ⇒
///     Quit() (mapped to <see cref="ClientSession.Abort" />); mismatch ⇒ reply <c>Result=1</c> and count the
///     strike, the THIRD consecutive strike disconnects (GL_504 in the legacy log); match ⇒ reply <c>Result=0</c>
///     and open the second-login gate (<c>mSecondLoginSort=0</c> → <c>MarkCharSelect</c>). The attempt is
///     verified against the Argon2id hash — the clear PIN exists only in the request (plan D10).
/// </summary>
public sealed class VerifyMousePinHandler(IAccountPinRepository pins)
    : IAsyncPacketHandler<VerifyMousePinRequest>
{
    /// <summary>Legacy <c>mSecondLoginTryNum == 3</c> (S04_MyWork02.cpp l.568): hardcoded there, hardcoded here.</summary>
    private const int MaxPinFailures = 3;

    public async ValueTask HandleAsync(VerifyMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        // Legacy: IsEmptyString(uMousePassword) => Quit — verifying a PIN that does not exist is a protocol
        // violation (the client is supposed to send op 13 to create one first).
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
