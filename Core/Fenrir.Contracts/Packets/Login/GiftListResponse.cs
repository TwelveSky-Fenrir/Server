using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_DEMAND_GIFT_RECV (LOGIN.h l.223-228): the 10-page gift list, answer to CL_GIFT_INFO_SEND (login
///     protocol report §4.25). GIFT_V2 is OFF in EU33 ⇒ MAX_GIFT_ITEM_PAGE_VALUE_NUM=2, so the matrix is
///     <c>int tGiftItem[10][2]</c> with <c>[i][0]=itemId</c> and <c>[i][1]=0</c>, flattened row-major here
///     (same convention as every other 2D array on this wire). Result is always 0 in the legacy path. No XOR.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.GiftList,
    ExpectedSize = 85)]
public readonly partial record struct GiftListResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    /// <summary>int[10][2] flattened row-major: [page*2+0]=itemId, [page*2+1]=0 (V1 path).</summary>
    [FixedArray(20)]
    public required int[] GiftItem { get; init; }
}
