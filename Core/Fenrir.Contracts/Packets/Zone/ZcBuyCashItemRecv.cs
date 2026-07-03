using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_BUY_CASH_ITEM_RECV (ZONE.h:630-637) — reply to CZ_BUY_CASH_ITEM_SEND (final confirmation after
///     the ts25extra IPC round trip). <c>Result</c> 0 = ok, 2/3/4 = errors, 60704 = shop-specific legacy
///     error code. Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.BuyCashItemRecv, ExpectedSize = 41)]
public readonly partial record struct ZcBuyCashItemRecv : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int CashSize { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    [FixedArray(6)] public required int[] Value { get; init; }
}
