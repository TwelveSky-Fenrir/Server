using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Result: 0=ok, 1=seller not found/moving, 2=stall closed, 3=empty slot, 7=stale UniqueNumber (4-6=funds/inventory). Fields zero on error.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyShopItem, ExpectedSize = 53)]
public readonly record struct BuyShopItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Cost { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
    [FixedArray(3)] public required int[] Socket { get; init; }
}
