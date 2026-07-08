using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// BloodIndex must be within [0, aBloodNum).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.BuyBloodMarkItem, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyBloodMarkItemRequest : IIncomingPacket<BuyBloodMarkItemRequest>
{
    public required int BloodIndex { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
