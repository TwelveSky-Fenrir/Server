using Fenrir.Application.Login.Pins;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Data.Security;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>op13 CL_CREATE_MOUSE_PASSWORD_SEND — first-time PIN creation; stored hashed (never in clear, unlike legacy), then opens char select.</summary>
public sealed class CreateMousePinHandler(IAccountPinRepository pins)
    : IAsyncPacketHandler<CreateMousePinRequest>
{
    public async ValueTask HandleAsync(CreateMousePinRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        // AllowedStates=[PinRequired] gates this past MarkAuthenticated, so AccountId is always set here.
        var accountId = loginSession.AccountId!.Value;

        if (!MousePinFormat.IsValid(packet.MousePassword))
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        // Legacy: creating over an existing PIN is a protocol violation (client should send op15/op14 instead).
        if (await pins.GetAsync(accountId, cancellationToken) is not null)
        {
            loginSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        try
        {
            var (hash, salt) = PasswordHasher.Hash(packet.MousePassword);
            await pins.SetAsync(accountId, hash, salt, cancellationToken);
        }
        catch (Exception)
        {
            // Legacy: storage failure is a silent Quit(), no reply (S04_MyWork02.cpp l.476-479).
            loginSession.Abort(DisconnectReason.Faulted);
            return;
        }

        loginSession.MarkCharSelect();
        session.Send(new CreateMousePinResponse { Result = 0, MousePassword = packet.MousePassword });
    }
}
