using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ZoneTransferFailure,
    ExpectedSize = 9, AllowedStates = [(byte)LoginSessionState.HandoverIssued])]
public readonly partial record struct ZoneTransferFailureRequest : IIncomingPacket<ZoneTransferFailureRequest>
{
}
