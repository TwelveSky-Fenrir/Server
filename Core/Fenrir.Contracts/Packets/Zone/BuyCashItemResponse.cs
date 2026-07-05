using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Result: 0=ok, 2/3/4=errors, 60704=shop-specific legacy error code.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyCashItem, ExpectedSize = 41)]
public readonly partial record struct BuyCashItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int CashSize { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
