using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_MAKE_ITEM138_SEND CLIENT.h:290-305 (6 paires, typedef propre); mort en M33/LNW33: opcode 138 non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MakeItem138, ExpectedSize = 61,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct CraftRecipe138Request : IIncomingPacket<CraftRecipe138Request>
{
    public required int Sort { get; init; }

    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int Page3 { get; init; }

    public required int Index3 { get; init; }

    public required int Page4 { get; init; }

    public required int Index4 { get; init; }

    public required int Page5 { get; init; }

    public required int Index5 { get; init; }

    public required int Page6 { get; init; }

    public required int Index6 { get; init; }
}
