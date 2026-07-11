using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ZoneTransferFailure,
    ExpectedSize = 9, AllowedStates = [(byte)LoginSessionState.HandoverIssued])]
public readonly partial record struct ZoneTransferFailureRequest : IIncomingPacket<ZoneTransferFailureRequest>
{
}
