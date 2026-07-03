using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     ITEM_LINK_INFO (STRUCT.h:1009-1014, 24 bytes) — clickable item link carried in the queue of the
///     six chat CZ/ZC packet families (USE_ITEM_LINK_V2 active in EU33). Declared outside STRUCT.h's
///     <c>pack(1)</c> regions but composed only of <c>int</c> members, so natural MSVC alignment matches
///     the packed layout exactly: zero padding.
/// </summary>
[FenrirWireType(24)]
public readonly partial record struct ItemLinkInfo : IFenrirWireType<ItemLinkInfo>
{
    public required int Index { get; init; }

    public required int Activity { get; init; }

    public required int Value { get; init; }

    [FixedArray(3)] public required int[] Socket { get; init; }
}
