using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyShopItem, ExpectedSize = 53)]
public readonly partial record struct BuyShopItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Cost { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
    [FixedArray(3)] public required int[] Socket { get; init; }
}
