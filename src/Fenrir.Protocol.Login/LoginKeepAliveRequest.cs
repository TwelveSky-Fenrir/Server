using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.LoginKeepAlive,
    ExpectedSize = 9,
    AllowedStates =
    [
        (byte)LoginSessionState.Authenticated, (byte)LoginSessionState.PinRequired,
        (byte)LoginSessionState.CharSelect, (byte)LoginSessionState.HandoverIssued
    ])]
public readonly partial record struct LoginKeepAliveRequest : IIncomingPacket<LoginKeepAliveRequest>
{
}
