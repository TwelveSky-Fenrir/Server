using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Byte-identical layout to <see cref="TradeStartResponse" />; this is the mid-trade slot-refresh variant.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeUpdate, ExpectedSize = 233)]
public readonly partial record struct TradeUpdateResponse : IOutgoingPacket
{
    public required int TradeMoney { get; init; }
    [FixedArray(32)] public required int[] Trade { get; init; }
    [FixedArray(24)] public required int[] TradeSocket { get; init; }
    public required int BigTradeMoney { get; init; }
}
