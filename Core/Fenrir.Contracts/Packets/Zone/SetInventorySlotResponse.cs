using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_SET_INVENTORY_ITEM_RECV (ZONE.h:1029-1034) — rewrites a full inventory slot client-side.
///     Emitters: box opening (via CZ_USE_INVENTORY_ITEM_SEND, 23), stack exchange, and several other
///     sites; unicast. No <see cref="Result" />-style field — pure state push.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SetInventorySlot,
    ExpectedSize = 33)]
public readonly partial record struct SetInventorySlotResponse : IOutgoingPacket
{
    public required int Page { get; init; }

    public required int Index { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
