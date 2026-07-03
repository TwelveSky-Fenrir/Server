using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     PSHOP_INFO (STRUCT.h:703-709, 1232 bytes) — personal-shop stall snapshot reused whole (padding
///     included) by CZ_START_PSHOP_SEND, ZC_START_PSHOP_RECV and ZC_UPDATE_PSHOP_RECV. Declared outside
///     STRUCT.h's <c>pack(1)</c> regions: natural MSVC alignment inserts 3 padding bytes after
///     <see cref="Name" /> (char[25], offset 4..28) so the following <c>int[5][5][9]</c> array lands on
///     a 4-byte boundary (offset 32) — part of the wire layout, mapped with <c>[Reserved(3)]</c>.
///     <c>mItemInfo[page][slot][k]</c> semantics (STRUCT.h:707, AddSearchItem S04_MyWork02.cpp:6501):
///     [0]=itemID, [1]=quantity, [2]=value, [3]=serial, [4]=price, [5]=page, [6]=index, [7]=xPost,
///     [8]=yPost (the vendor's real inventory slot / visual position).
/// </summary>
[FenrirWireType(1232)]
public readonly partial record struct PshopInfo : IFenrirWireType<PshopInfo>
{
    public required uint UniqueNumber { get; init; }

    [FixedString(25)] public required string Name { get; init; }

    [Reserved(3)] [FixedArray(225)] public required int[] ItemInfo { get; init; }

    [FixedArray(75)] public required int[] SocketInfo { get; init; }
}
