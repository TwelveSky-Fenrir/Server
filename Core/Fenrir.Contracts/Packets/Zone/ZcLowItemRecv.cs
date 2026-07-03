using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_LOW_ITEM_RECV (ZONE.h:531) — same typedef as <see cref="ZcExchangeItemRecv" /> (29). Reply to
///     CZ_LOW_ITEM_SEND (28); unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.LowItemRecv, ExpectedSize = 33)]
public readonly partial record struct ZcLowItemRecv : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
