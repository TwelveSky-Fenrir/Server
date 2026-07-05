using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

// Legal only in HandoverIssued: legacy handler Quit()s otherwise. Rolls the session back to char-select.
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ZoneTransferFailure,
    ExpectedSize = 9, AllowedStates = [(byte)LoginSessionState.HandoverIssued])]
public readonly partial record struct ZoneTransferFailureRequest : IIncomingPacket<ZoneTransferFailureRequest>
{
}
