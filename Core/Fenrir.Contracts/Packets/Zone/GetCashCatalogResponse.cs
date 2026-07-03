using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GET_CASH_ITEM_INFO_RECV (ZONE.h:1036-1041) — reply to CZ_GET_CASH_ITEM_INFO_SEND, sent only if
///     the client's cache version differs from <c>mCashInfo-&gt;mVersion</c>. The largest packet in the
///     protocol (12 809 bytes), NOT compressed (no <c>createZPacket</c>, verified). Dimensions
///     <c>[MAX_CASH_TYPE=4][MAX_CASH_PAGE=20][MAX_CASH_ITEM_PER_PAGE=10][MAX_CASH_ITEM_DETIAL=4]</c>
///     flattened row-major to 3200 ints. Source: <c>mCashInfo-&gt;mCash</c> (CASH_INFO, STRUCT.h:1436-1446,
///     pack(1) region). <c>Result</c> is always 0 in live code. Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetCashCatalog,
    ExpectedSize = 12809)]
public readonly partial record struct GetCashCatalogResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Version { get; init; }
    [FixedArray(3200)] public required int[] CashItemInfo { get; init; }
}
