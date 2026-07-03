using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_EXCHANGE_ITEM_RECV (ZONE.h:526-531) — typedef SHARED with <see cref="ZcHighItemRecv" /> (30)
///     and <see cref="ZcLowItemRecv" /> (31): identical layout, distinct C# contracts. Reply to
///     CZ_EXCHANGE_ITEM_SEND (26); unicast. <see cref="Value" /> (6 ints) describes the resulting item
///     (index, quantity, value, position...).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ExchangeItemRecv,
    ExpectedSize = 33)]
public readonly partial record struct ZcExchangeItemRecv : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
