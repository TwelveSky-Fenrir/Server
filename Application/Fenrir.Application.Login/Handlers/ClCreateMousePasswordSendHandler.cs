using Fenrir.Application.Login.Pins;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Domain.Security;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op13 CL_CREATE_MOUSE_PASSWORD_SEND — first-time mouse-PIN creation (login protocol report §4.13), the
///     mandatory path for a fresh account in prod EU33 (P2ndPassword=1). Legacy preconditions all Quit() with no
///     reply, mapped here to <see cref="ClientSession.Abort" />: PIN already validated (covered by
///     <c>AllowedStates=[PinRequired]</c>), a PIN already exists in storage, invalid format, storage failure (the
///     legacy result-1 reply is commented out in the C++ — failure is a silent disconnect there too). Success
///     stores the PIN HASHED (plan D10 — never in clear, unlike the legacy), echoes the request's clear PIN back
///     (byte-compatible: the echo never comes from storage) and opens the second-login gate
///     (<c>mSecondLoginSort=0</c> → <c>MarkCharSelect</c>).
/// </summary>
public sealed class ClCreateMousePasswordSendHandler(IAccountPinRepository pins)
    : IAsyncPacketHandler<ClCreateMousePasswordSend>
{
    public async ValueTask HandleAsync(ClCreateMousePasswordSend packet, IPacketSession session,
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

        // Legacy: !IsEmptyString(uMousePassword) => Quit — creating over an existing PIN is a protocol violation
        // (the client is supposed to send op 15, or op 14 to change).
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
            // Legacy UpdateMousePassword failure: the B_CREATE_MOUSE_PASSWORD_RECV(1, c0000) reply is commented
            // out in the C++ (S04_MyWork02.cpp l.476-479) — the real behaviour is a silent Quit(). Same here.
            loginSession.Abort(DisconnectReason.Faulted);
            return;
        }

        loginSession.MarkCharSelect();
        session.Send(new LcCreateMousePasswordRecv { Result = 0, MousePassword = packet.MousePassword });
    }
}
