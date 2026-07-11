using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

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
