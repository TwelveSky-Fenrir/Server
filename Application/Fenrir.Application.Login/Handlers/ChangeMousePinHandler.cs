using Fenrir.Application.Login.Pins;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Domain.Security;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op14 CL_CHANGE_MOUSE_PASSWORD_SEND — change the PIN by presenting the current one (login protocol report
///     §4.14). Only reachable at the PIN screen (legacy Quit()s once <c>IsSecondLogin()</c> is true, hence
///     <c>AllowedStates=[PinRequired]</c>). Legacy: no PIN in storage or invalid formats ⇒ Quit(); wrong current
///     PIN ⇒ reply <c>Result=1, "0000"</c> + strike (3rd disconnects — same counter as op 15); storage failure ⇒
///     reply <c>Result=2, "0000"</c> WITHOUT disconnecting; success ⇒ reply <c>Result=0</c> with the NEW PIN
///     echoed in clear from the request, and — legacy quirk worth keeping — the change also VALIDATES the PIN
///     (<c>mSecondLoginSort=0</c>, S04_MyWork02.cpp l.532), so the session proceeds to CharSelect directly.
/// </summary>
public sealed class ChangeMousePinHandler(IAccountPinRepository pins)
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

        // Legacy: IsEmptyString(uMousePassword) => Quit — nothing to change yet, the client must create (op 13).
        var storedPin = await pins.GetAsync(accountId, cancellationToken);
        if (storedPin is null)
        {
            loginSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (!MousePinFormat.IsValid(packet.MousePassword) || !MousePinFormat.IsValid(packet.ChangeMousePassword))
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        if (!PasswordHasher.Verify(packet.MousePassword, storedPin.PinHash, storedPin.PinSalt))
        {
            session.Send(new ChangeMousePinResponse { Result = 1, MousePassword = ZeroPin });
            if (loginSession.RegisterPinFailure() >= MaxPinFailures)
                loginSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        try
        {
            var (hash, salt) = PasswordHasher.Hash(packet.ChangeMousePassword);
            await pins.SetAsync(accountId, hash, salt, cancellationToken);
        }
        catch (Exception)
        {
            // Legacy: UpdateMousePassword failure replies 2 and keeps the connection alive (l.525-530).
            session.Send(new ChangeMousePinResponse { Result = 2, MousePassword = ZeroPin });
            return;
        }

        // The legacy GL_503 UDP log shipped both PINs in clear to ts25gamelog; deliberately not reproduced
        // (plan D9: structured logs, and never a clear PIN anywhere server-side).
        loginSession.MarkCharSelect();
        session.Send(new ChangeMousePinResponse { Result = 0, MousePassword = packet.ChangeMousePassword });
    }
}
