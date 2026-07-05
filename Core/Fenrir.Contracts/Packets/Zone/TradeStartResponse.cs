using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     Cross-delivered: each side receives the OTHER player's offer; TradeSocket stays wire-significant
///     (USE_SOCKET_GEM undef).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeStart, ExpectedSize = 233)]
public readonly partial record struct TradeStartResponse : IOutgoingPacket
{
    public required int TradeMoney { get; init; }
    [FixedArray(32)] public required int[] Trade { get; init; }
    [FixedArray(24)] public required int[] TradeSocket { get; init; }
    public required int BigTradeMoney { get; init; }
}
