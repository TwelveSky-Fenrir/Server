using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_BUY_PSHOP_RECV (ZONE.h:567-575) — reply to CZ_BUY_PSHOP_SEND. <c>Result</c> 0 = ok
///     (<c>Page</c>/<c>Index</c> = the buyer's inventory slot), 1 = seller not found/moving, 2 = stall
///     closed, 3 = empty slot, 7 = stale <c>UniqueNumber</c> (plus 4-6: funds/inventory). On error all
///     data fields are zero. Unicast.
/// </summary>
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
