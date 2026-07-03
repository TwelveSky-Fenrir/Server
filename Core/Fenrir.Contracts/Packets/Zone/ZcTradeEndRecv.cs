using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRADE_END_RECV (ZONE.h:707-710). <c>Result=0</c> = trade CONCLUDED successfully (end of W_TRADE_MENU_SEND,
///     l.9011/9014); <c>Result=1</c> = trade CANCELLED (W_TRADE_END_SEND, l.9030/9051) — inverse of what the
///     02_zc_inventory pass recorded.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeEndRecv, ExpectedSize = 5)]
public readonly partial record struct ZcTradeEndRecv : IOutgoingPacket
{
    public required int Result { get; init; }
}
