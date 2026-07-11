using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyBloodMarkItem, ExpectedSize = 41)]
public readonly partial record struct BuyBloodMarkItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int BloodCoin { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
