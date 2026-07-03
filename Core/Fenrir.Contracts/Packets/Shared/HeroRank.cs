using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     HERO_RANK (STRUCT.h:678-682, 680 bytes) — hero ranking snapshot shared by ZC_HERORANK_INFO_RECV
///     and ZC_HERORANK_UPDATE_RECV (ZC 148/150). Declared outside pack(1) but <c>char[4][10][13]</c>
///     (520 bytes) is already a multiple of 4, so the following <c>int[4][10]</c> needs no realignment
///     → zero padding. Dimensions locked to <c>MAX_TRIBE_NUM = 4</c> and
///     <c>MAX_HERO_RANK_AVATAR_NUM = 10</c>; both arrays flatten row-major with index = tribe*10 + rank.
/// </summary>
[FenrirWireType(680)]
public readonly partial record struct HeroRank : IFenrirWireType<HeroRank>
{
    [FixedArray(40)] [FixedString(13)] public required string[] Name { get; init; }

    [FixedArray(40)] public required int[] Point { get; init; }
}
