using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_TRAP_CHECK_SEND Server/Header/Protocol/CLIENT.h:431-435 ; mort en M33 : opcode non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TrapCheck, ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TrapCheckRequest : IIncomingPacket<TrapCheckRequest>
{
    public required int TrapIndex { get; init; }
    [FixedArray(3)] public required float[] Location { get; init; }
}
