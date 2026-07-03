using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRADE_STATE_RECV (ZONE.h:694-700) — distinct C++ struct, byte-identical layout to
///     <see cref="ZcTradeStartRecv" />; emitted on slot refresh during an active trade (S07_MyGame04). Not to be confused
///     with the dead local
///     ZC_TRADE_START_RECV2/STATE_RECV2 (ST_TRADE_INFO) variant declared at S05_MyTransfer.cpp:858-878 — no call-site,
///     never implemented.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeStateRecv, ExpectedSize = 233)]
public readonly partial record struct ZcTradeStateRecv : IOutgoingPacket
{
    public required int TradeMoney { get; init; }
    [FixedArray(32)] public required int[] Trade { get; init; }
    [FixedArray(24)] public required int[] TradeSocket { get; init; }
    public required int BigTradeMoney { get; init; }
}
