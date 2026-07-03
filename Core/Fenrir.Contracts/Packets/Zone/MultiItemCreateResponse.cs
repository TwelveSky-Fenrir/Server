using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_MULTI_ITEM_CREATE_RECV (ZONE.h:1096-1106) — grouped creation of up to 8 items. Emitters: the
///     MISSION_COMPLETE handler and 6 multi-item-pack-opening sites (via CZ_USE_INVENTORY_ITEM_SEND, 23);
///     unicast. Server-side BIT-PACKED encoding: <see cref="Page" />/<see cref="Index1" />/
///     <see cref="Index2" />/<see cref="Xy1" />/<see cref="Xy2" /> compact page/slot/coordinates of the 8
///     items into ints (4 slots per int for Index/Xy) — the contract carries the raw ints, packing stays
///     in the handler.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MultiItemCreate,
    ExpectedSize = 73)]
public readonly partial record struct MultiItemCreateResponse : IOutgoingPacket
{
    public required int Num { get; init; }

    public required int Page { get; init; }

    public required int Index1 { get; init; }

    public required int Index2 { get; init; }

    public required int Xy1 { get; init; }

    public required int Xy2 { get; init; }

    [FixedArray(8)] public required int[] ItemIndex { get; init; }

    [FixedArray(4)] public required int[] Value { get; init; }
}
