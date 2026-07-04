using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

// Flat index = tribe*10 + rank (MAX_TRIBE_NUM=4 x MAX_HERO_RANK_AVATAR_NUM=10).
[FenrirWireType(680)]
public readonly partial record struct HeroRank : IFenrirWireType<HeroRank>
{
    [FixedArray(40)] [FixedString(13)] public required string[] Name { get; init; }

    [FixedArray(40)] public required int[] Point { get; init; }
}
