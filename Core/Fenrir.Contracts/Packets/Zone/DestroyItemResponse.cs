using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_DESTROY_ITEM_RECV (ZONE.h:991-996) — reply to CZ_DESTROY_ITEM_SEND (89); unicast.
///     <see cref="Money" /> is the refund (dissolution); <see cref="Value" /> is the emptied slot /
///     compensation stone.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DestroyItem, ExpectedSize = 33)]
public readonly partial record struct DestroyItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Money { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
