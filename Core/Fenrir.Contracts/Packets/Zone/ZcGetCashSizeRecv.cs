using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_GET_CASH_SIZE_RECV (ZONE.h:624-628) — reply to CZ_GET_CASH_SIZE_SEND. <c>Sort</c> echoes the
///     request (client-side UI routing). Field order: balance BEFORE sort. Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetCashSizeRecv, ExpectedSize = 9)]
public readonly partial record struct ZcGetCashSizeRecv : IOutgoingPacket
{
    public required int CashSize { get; init; }
    public required int Sort { get; init; }
}
