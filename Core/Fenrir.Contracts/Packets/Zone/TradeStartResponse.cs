using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRADE_START_RECV (ZONE.h:686-692) — cross-delivered: each player receives the OTHER's offer
///     (S04_MyWork02.cpp:8634-8637). USE_SOCKET_GEM undef: TradeSocket is never zeroed in the compiled path, so it
///     remains wire-significant. See <see cref="TradeUpdateResponse" /> for the identical-layout refresh variant.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeStart, ExpectedSize = 233)]
public readonly partial record struct TradeStartResponse : IOutgoingPacket
{
    public required int TradeMoney { get; init; }
    [FixedArray(32)] public required int[] Trade { get; init; }
    [FixedArray(24)] public required int[] TradeSocket { get; init; }
    public required int BigTradeMoney { get; init; }
}
