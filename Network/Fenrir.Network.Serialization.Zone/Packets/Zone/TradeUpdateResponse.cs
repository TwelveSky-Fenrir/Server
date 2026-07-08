using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Byte-identical layout to <see cref="TradeStartResponse" />; this is the mid-trade slot-refresh variant.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeUpdate, ExpectedSize = 233)]
public readonly partial record struct TradeUpdateResponse : IOutgoingPacket
{
    public required int TradeMoney { get; init; }
    [FixedArray(32)] public required int[] Trade { get; init; }
    [FixedArray(24)] public required int[] TradeSocket { get; init; }
    public required int BigTradeMoney { get; init; }
}
