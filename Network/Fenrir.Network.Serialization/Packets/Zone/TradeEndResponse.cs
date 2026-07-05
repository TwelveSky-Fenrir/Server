using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Result: 0 = trade concluded, 1 = trade cancelled.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeEnd, ExpectedSize = 5)]
public readonly partial record struct TradeEndResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
