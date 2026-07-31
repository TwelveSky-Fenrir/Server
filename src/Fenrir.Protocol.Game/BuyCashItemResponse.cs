using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyCashItem, ExpectedSize = 41)]
public readonly partial record struct BuyCashItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int CashSize { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
