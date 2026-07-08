using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

// PinRequired allowed too: legacy op12 is accepted at any validated state; the handler enforces the PIN gate.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.LoginKeepAlive,
    ExpectedSize = 9,
    AllowedStates =
    [
        (byte)LoginSessionState.Authenticated, (byte)LoginSessionState.PinRequired, (byte)LoginSessionState.CharSelect
    ])]
public readonly partial record struct LoginKeepAliveRequest : IIncomingPacket<LoginKeepAliveRequest>
{
}
