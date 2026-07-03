using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

// PinRequired is allowed too: the legacy op 12 is a pure keepalive accepted whenever mCheckValidState is set
// (S04_MyWork02.cpp l.437-447), which includes the window where the PIN is still pending — the handler is what
// makes sure the keepalive never promotes a PinRequired session past the PIN gate.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.LoginKeepAlive,
    ExpectedSize = 9,
    AllowedStates =
    [
        (byte)LoginSessionState.Authenticated, (byte)LoginSessionState.PinRequired, (byte)LoginSessionState.CharSelect
    ])]
public readonly partial record struct LoginKeepAliveRequest : IIncomingPacket<LoginKeepAliveRequest>
{
}
