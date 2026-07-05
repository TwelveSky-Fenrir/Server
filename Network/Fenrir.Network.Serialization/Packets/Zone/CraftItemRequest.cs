using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Sort is an MK_* recipe constant; unknown Sort -> Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CraftItem, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CraftItemRequest : IIncomingPacket<CraftItemRequest>
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
