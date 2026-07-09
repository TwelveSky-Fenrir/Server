using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

// Legal only in HandoverIssued: legacy handler Quit()s otherwise. Rolls the session back to char-select.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ZoneTransferFailure,
    ExpectedSize = 9, AllowedStates = [(byte)LoginSessionState.HandoverIssued])]
public readonly record struct ZoneTransferFailureRequest : IIncomingPacket<ZoneTransferFailureRequest>
{
}
