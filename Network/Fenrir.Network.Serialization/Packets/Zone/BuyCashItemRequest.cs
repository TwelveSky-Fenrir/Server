using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Version must match the server's cash catalog version; Quit() if two purchases arrive < 200ms apart.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyCashItem, ExpectedSize = 49,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyCashItemRequest : IIncomingPacket<BuyCashItemRequest>
{
    public required int CostInfoIndex { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
    public required int Version { get; init; }
}
