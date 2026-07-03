using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     CL_CLIENT_OK_FOR_LOGIN_SEND (op12): client ack after receiving the login train — in the legacy a pure
///     keepalive (<c>UpdateUseTime()</c>, no reply, S04_MyWork02.cpp l.437-447). No I/O, no reply — stays inline
///     (D-04 microsecond budget). The CharSelect promotion only happens from <c>Authenticated</c>: when the
///     session is parked in <c>PinRequired</c> (P2ndPassword=1) this ack must NOT open the PIN gate — only
///     ops 13/14/15 can — so there it degrades to the legacy keepalive no-op.
/// </summary>
public sealed class ClClientOkForLoginSendHandler : IInlinePacketHandler<ClClientOkForLoginSend>
{
    public void Handle(in ClClientOkForLoginSend packet, IPacketSession session)
    {
        var loginSession = (LoginClientSession)session;
        if (loginSession.State == LoginSessionState.Authenticated)
            loginSession.MarkCharSelect();
    }
}
