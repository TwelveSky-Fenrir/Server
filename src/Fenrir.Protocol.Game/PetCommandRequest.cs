using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_PAT_ACTION_SEND Server/Header/Protocol/CLIENT.h:345-348 ; mort en M33 : opcode non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PatAction, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct PetCommandRequest : IIncomingPacket<PetCommandRequest>
{
    public required int Sort { get; init; }
}
