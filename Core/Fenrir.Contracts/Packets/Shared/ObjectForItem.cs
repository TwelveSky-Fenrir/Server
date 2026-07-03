using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     OBJECT_FOR_ITEM (STRUCT.h:941-955, 84 bytes) — dropped-item replication snapshot carried by
///     ZC_ITEM_ACTION_RECV. Declared outside STRUCT.h's <c>pack(1)</c> regions: natural MSVC alignment
///     inserts 2 padding bytes after <see cref="PartyName" /> (char[13], offset 41..53) so that the
///     following <c>int iDropSort</c> lands on a 4-byte boundary (offset 56) — part of the wire layout,
///     mapped with <c>[Reserved(2)]</c>. <c>iSocketGem</c> is transmitted even though
///     <c>USE_SOCKET_GEM</c> is <c>#undef</c> in EU33 (always zeros on the wire, never conditioned by
///     an <c>#ifdef</c>).
/// </summary>
[FenrirWireType(84)]
public readonly partial record struct ObjectForItem : IFenrirWireType<ObjectForItem>
{
    public required int Index { get; init; }

    public required int Quantity { get; init; }

    public required int Value { get; init; }

    public required int SerialNumber { get; init; }

    [FixedArray(3)] public required float[] Location { get; init; }

    [FixedString(13)] public required string Master { get; init; }

    [FixedString(13)] public required string PartyName { get; init; }

    [Reserved(2)] public required int DropSort { get; init; }

    public required uint CreateTime { get; init; }

    public required uint PresentTime { get; init; }

    public required int CreateState { get; init; }

    [FixedArray(3)] public required int[] SocketGem { get; init; }
}
