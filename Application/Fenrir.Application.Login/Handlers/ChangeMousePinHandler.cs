using Fenrir.Application.Login.Pins;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Domain.Security;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>op14 CL_CHANGE_MOUSE_PASSWORD_SEND — legacy quirk: a successful change also validates the PIN (S04_MyWork02.cpp l.532), so the session goes straight to CharSelect.</summary>
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

        // No stored PIN => Quit; client must create one first (op13).
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
            // Legacy: storage failure replies 2 without disconnecting (S04_MyWork02.cpp l.525-530).
            session.Send(new ChangeMousePinResponse { Result = 2, MousePassword = ZeroPin });
            return;
        }

        loginSession.MarkCharSelect();
        session.Send(new ChangeMousePinResponse { Result = 0, MousePassword = packet.ChangeMousePassword });
    }
}
