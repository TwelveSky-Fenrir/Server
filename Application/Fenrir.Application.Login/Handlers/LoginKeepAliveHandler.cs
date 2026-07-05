using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op12 CL_CLIENT_OK_FOR_LOGIN_SEND — only promotes Authenticated to CharSelect; must not bypass the PinRequired
///     gate (only ops 13/14/15 may open it).
/// </summary>
public sealed class LoginKeepAliveHandler : IInlinePacketHandler<LoginKeepAliveRequest>
{
    public void Handle(in LoginKeepAliveRequest packet, IPacketSession session)
    {
        var loginSession = (LoginClientSession)session;
        if (loginSession.State == LoginSessionState.Authenticated)
            loginSession.MarkCharSelect();
    }
}
