using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     PROXY_SHOP_USER_INFO (STRUCT.h:1752-1760, 824 bytes) — full deputy-pshop stall snapshot carried by the
///     ZC_GET/SET_DEPUTY_PSHOP_RECV family (ZONE.h:1306/1312). Declared outside STRUCT.h's <c>pack(1)</c>
///     regions, so the compiler's natural alignment inserts 3 padding bytes after <see cref="AvatarName" />
///     (char[13]) before the int-aligned item array — part of the wire layout, mapped with <c>[Reserved(3)]</c>.
///     <c>tItem[MAX_PSHOP_PAGE_NUM=5][MAX_PSHOP_SLOT_NUM=5]</c> flattens row-major to 25 nested
///     <see cref="ProxyShopItem" /> (DEFINE.h:321-322), <c>tSocket[5][5][MAX_GEM_NUM=3]</c> to 75 ints.
///     Layout: name 0..12, pad 13..15, items 16..515, sockets 516..815, money 816, big-money 820.
/// </summary>
[FenrirWireType(824)]
public readonly partial record struct ProxyShopUserInfo : IFenrirWireType<ProxyShopUserInfo>
{
    [FixedString(13)] public required string AvatarName { get; init; }

    [Reserved(3)] [FixedArray(25)] public required ProxyShopItem[] Items { get; init; }

    [FixedArray(75)] public required int[] Sockets { get; init; }

    public required int Money { get; init; }

    public required int BigMoney { get; init; }
}
