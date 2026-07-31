using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.GiftList,
    ExpectedSize = 9,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct GiftListRequest : IIncomingPacket<GiftListRequest>
{
}
