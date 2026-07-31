using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_MAKE_ITEM134_SEND CLIENT.h:265-272 (typedef SANS tLuck); mort en M33/LNW33: opcode 134 non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MakeItem134, ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct CraftPair134Request : IIncomingPacket<CraftPair134Request>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }
}
