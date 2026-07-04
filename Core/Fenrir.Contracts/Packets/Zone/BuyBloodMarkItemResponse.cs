using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Result: 0=ok, 1=catalog unavailable, 2/3=funds/inventory errors.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyBloodMarkItem, ExpectedSize = 41)]
public readonly partial record struct BuyBloodMarkItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int BloodCoin { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
