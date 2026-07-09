using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Result: 0 = trade concluded, 1 = trade cancelled.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeEnd, ExpectedSize = 5)]
public readonly record struct TradeEndResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
