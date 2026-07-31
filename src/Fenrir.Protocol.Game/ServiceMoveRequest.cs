using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_SERVICE_MOVE_SEND Server/Header/Protocol/CLIENT.h:155-161 (corps vide) ; mort en M33 : opcode non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ServiceMove, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ServiceMoveRequest : IIncomingPacket<ServiceMoveRequest>
{
}
