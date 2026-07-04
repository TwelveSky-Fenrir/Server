using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(1232)]
public readonly partial record struct PshopInfo : IFenrirWireType<PshopInfo>
{
    public required uint UniqueNumber { get; init; }

    [FixedString(25)] public required string Name { get; init; }

    // 3-byte pad after Name (char[25]) to align ItemInfo. Per slot: itemID,quantity,value,serial,
    // price,page,index,xPost,yPost (9 ints/slot, [page][slot][9]).
    [Reserved(3)] [FixedArray(225)] public required int[] ItemInfo { get; init; }

    [FixedArray(75)] public required int[] SocketInfo { get; init; }
}
