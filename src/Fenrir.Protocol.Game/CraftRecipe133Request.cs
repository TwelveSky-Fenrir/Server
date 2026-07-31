using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_MAKE_ITEM133_SEND CLIENT.h:278-289; mort en M33/LNW33: opcode 133 non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MakeItem133, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct CraftRecipe133Request : IIncomingPacket<CraftRecipe133Request>
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
}
